using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Options;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Viewmodel of the third (last) wizard step: the adjusting itself.
    ///
    /// The adjusting is performed by <see cref="AdjustSession"/>; what is left here is what
    /// the wizard is responsible for: showing the progress, cancelling the session and
    /// closing the window when it is over.
    /// </summary>
    public class PerformingViewModel : ChainViewModel
    {
        private readonly CancellationTokenSource _cts = new();

        private readonly AdjustContext _context;
        private readonly IWizardHost _host;
        private readonly List<string> _subjectFilePaths;
        private readonly NamespaceReplaceRegex _replaceRegex;
        private readonly bool _openFilesToEnableUndo;

        private RelayCommand? _cancelCommand;
        private System.Threading.Tasks.Task<AdjustSessionOutcome>? _task;

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
        /// <param name="parameters">Parameters of this step.</param>
        public PerformingViewModel(
            AdjustContext context,
            IWizardHost host,
            PerformingParameters parameters
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            _context = context;
            _host = host;
            _subjectFilePaths = parameters.SubjectFilePaths;
            _replaceRegex = parameters.ReplaceRegex;
            _openFilesToEnableUndo = parameters.OpenFilesToEnableUndo;
            _progressMessage = string.Empty;
        }

        /// <inheritdoc/>
        public override async System.Threading.Tasks.Task StartAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            //var undoManager = (await the service provider.GetServiceAsync(typeof(SVsLinkedUndoTransactionManager))) as IVsLinkedUndoTransactionManager;
            //ErrorHandler.ThrowOnFailure(
            //    undoManager!.OpenLinkedUndo((uint)LinkedTransactionFlagsEnum.Global, "Adjusting Namespaces")
            //    );

            //int transactionCount = 0;
            //ErrorHandler.ThrowOnFailure(
            //    undoManager.CountOpenTransactions(ref transactionCount)
            //    );

            _task = new AdjustSession(_context, _replaceRegex, _openFilesToEnableUndo)
                .RunAsync(
                    _subjectFilePaths,
                    //this Progress is built on the main thread (see the switch above) and
                    //therefore marshals the reports of the session back to it: the binding
                    //of the progress line is never touched from a background thread
                    new Progress<AdjustProgress>(p => ProgressMessage = p.Message),
                    _cts.Token
                    );

            var outcome = await _task;

            //ErrorHandler.ThrowOnFailure(undoManager.CloseLinkedUndo());

            ProgressMessage = outcome == AdjustSessionOutcome.Cancelled
                ? "Cancelled"
                : "Completed"
                ;

            await System.Threading.Tasks.Task.Delay(750);

            General.Instance.FilesAdjusted += _subjectFilePaths.Count;

            _cts.Dispose();

            _host.Close();
        }
    }

}
