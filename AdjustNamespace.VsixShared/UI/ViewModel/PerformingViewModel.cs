using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Adjusting.Report;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Options;
using AdjustNamespace.TextReferences;
using AdjustNamespace.UI.StepFactory;
using AdjustNamespace.VisualStudio;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Viewmodel of the performing wizard step: the adjusting itself.
    ///
    /// The adjusting is performed by <see cref="AdjustSession"/>; what is left here is what
    /// the wizard is responsible for: showing the progress, cancelling the session,
    /// wrapping the run in a global linked undo transaction and moving on to the report
    /// (or closing the window, if the user does not want to see it) when it is over.
    /// </summary>
    public class PerformingViewModel : ChainViewModel
    {
        private readonly CancellationTokenSource _cts = new();

        private readonly AdjustContext _context;
        private readonly IWizardHost _host;
        private readonly IStepFactory<ReportParameters> _reportStepFactory;
        private readonly List<string> _subjectFilePaths;
        private readonly NamespaceReplaceRegex _replaceRegex;
        private readonly IReadOnlyList<AdjustBlock> _blocked;
        private readonly TextReferenceScanResult _textReferences;

        private RelayCommand? _cancelCommand;
        private System.Threading.Tasks.Task<AdjustReport>? _task;

        private string _progressMessage;

        /// <summary>
        /// Progress line of the step.
        /// </summary>
        public string ProgressMessage
        {
            get => _progressMessage;
            private set
            {
                _progressMessage = value;
                OnPropertyChanged(nameof(ProgressMessage));
            }
        }

        /// <summary>
        /// Cancel the adjusting. The already applied changes are not reverted.
        /// </summary>
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    _cancelCommand = new RelayCommand(
                        a =>
                        {
                            if (_task != null && !_cts.IsCancellationRequested)
                            {
                                _cts.Cancel();
                            }
                        },
                        r => !_cts.IsCancellationRequested
                        );
                }

                return _cancelCommand;
            }
        }

        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="host">The window this step is shown in.</param>
        /// <param name="reportStepFactory">The step that shows the report of the run.</param>
        /// <param name="parameters">Parameters of this step.</param>
        public PerformingViewModel(
            AdjustContext context,
            IWizardHost host,
            IStepFactory<ReportParameters> reportStepFactory,
            PerformingParameters parameters
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (reportStepFactory is null)
            {
                throw new ArgumentNullException(nameof(reportStepFactory));
            }

            _context = context;
            _host = host;
            _reportStepFactory = reportStepFactory;
            _subjectFilePaths = parameters.SubjectFilePaths;
            _replaceRegex = parameters.ReplaceRegex;
            _blocked = parameters.Blocked;
            _textReferences = parameters.TextReferences;
            _progressMessage = string.Empty;
        }

        /// <inheritdoc/>
        public override async System.Threading.Tasks.Task StartAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var report = await RunAsync();

            _cts.Dispose();

            General.Instance.FilesAdjusted += report.AdjustedCount;

            ShowInErrorList(report);

            var rolledBack = report.Outcome == AdjustSessionOutcome.Failed;

            if (!IsReportWanted(report))
            {
                ProgressMessage = report.Outcome == AdjustSessionOutcome.Cancelled
                    ? "Cancelled"
                    : "Completed"
                    ;

                await System.Threading.Tasks.Task.Delay(750);

                _host.Close();

                return;
            }

            await _reportStepFactory.CreateAsync(
                new ReportParameters(report, rolledBack)
                );
        }

        /// <summary>
        /// Run the session inside a global linked undo transaction. The transaction is closed
        /// (and the changes become one undo unit) before the report is shown.
        /// </summary>
        private async System.Threading.Tasks.Task<AdjustReport> RunAsync()
        {
            using var undo = LinkedUndoTransaction.Open("Adjust Namespaces");

            try
            {
                _task = new AdjustSession(_context, _replaceRegex, _blocked, null, _textReferences, General.Instance.RunBuildCheck)
                    .RunAsync(
                        _subjectFilePaths,
                        //this Progress is built on the main thread (see the switch above) and
                        //therefore marshals the reports of the session back to it: the binding
                        //of the progress line is never touched from a background thread
                        new Progress<AdjustProgress>(p => ProgressMessage = p.Message),
                        _cts.Token
                        );

                var report = await _task;

                //CloseLinkedUndo (via Dispose) commits one undo unit for whatever was applied.
                //Do not Abort on cancel: AbortLinkedUndo rolls the changes back, and the
                //wizard promises that a cancel keeps the already applied edits.
                //A failed session is rolled back: the file it stopped at may be half changed.
                if (report.Outcome == AdjustSessionOutcome.Failed)
                {
                    undo.Abort();
                }

                return report;
            }
            catch
            {
                //unexpected failure: roll the whole transaction back
                undo.Abort();
                throw;
            }
        }

        /// <summary>
        /// What the run has not fixed stays visible after the window is closed.
        /// </summary>
        private static void ShowInErrorList(AdjustReport report)
        {
            try
            {
                ErrorListReporter.Show(report);
            }
            catch (Exception excp)
            {
                //the Error List is a convenience: a failure of it must not turn a finished run into a failed one
                Logging.LogVS(excp);
            }
        }

        /// <summary>
        /// The user wants to see this report, see <see cref="ReportVisibility"/>.
        /// A failed run is always shown: there is a message the user has to read.
        /// </summary>
        private static bool IsReportWanted(AdjustReport report)
        {
            if (report.Outcome == AdjustSessionOutcome.Failed)
            {
                return true;
            }

            switch (General.Instance.ShowReport)
            {
                case ReportVisibility.Never:
                    return false;

                case ReportVisibility.WhenSomethingIsLeft:
                    return report.HasLeftovers;

                default:
                    return true;
            }
        }
    }

}
