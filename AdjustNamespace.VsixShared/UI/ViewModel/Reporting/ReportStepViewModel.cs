using AdjustNamespace.Adjusting.Report;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.VisualStudio;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace AdjustNamespace.UI.ViewModel.Reporting
{
    /// <summary>
    /// Viewmodel of the last wizard step: the report of the run, shown instead of closing the
    /// window right after it.
    ///
    /// Everything it tells is decided by the core (<see cref="AdjustReport"/> and
    /// <see cref="AdjustReportSections"/>); what is left here is the buttons.
    /// </summary>
    public class ReportStepViewModel : ChainViewModel
    {
        private readonly IWizardHost _host;
        private readonly AdjustReport _report;
        private readonly bool _rolledBack;

        private ICommand? _closeCommand;
        private ICommand? _copyCommand;
        private ICommand? _saveCommand;
        private ICommand? _openLogCommand;
        private ICommand? _openNodeCommand;

        /// <summary>
        /// The summary line of the report.
        /// </summary>
        public string Summary => _report.Summary;

        /// <summary>
        /// What the user has to know besides the summary (the run has been cancelled, has failed),
        /// or an empty string.
        /// </summary>
        public string Note
        {
            get
            {
                switch (_report.Outcome)
                {
                    case AdjustSessionOutcome.Cancelled:
                        return "The run has been cancelled. The changes which were applied before the cancel are kept.";

                    case AdjustSessionOutcome.Failed:
                        return $"The run has failed: {_report.FailureMessage}"
                            + (_rolledBack ? Environment.NewLine + "The changes of the run have been rolled back." : string.Empty);

                    default:
                        return string.Empty;
                }
            }
        }

        /// <summary>
        /// There is a note to show.
        /// </summary>
        public bool HasNote => Note.Length > 0;

        /// <summary>
        /// The sections of the report: what has been adjusted and what has not.
        /// </summary>
        public ObservableCollection<ReportNodeViewModel> Sections
        {
            get;
        }

        /// <summary>
        /// Close the wizard.
        /// </summary>
        public ICommand CloseCommand
        {
            get
            {
                if (_closeCommand == null)
                {
                    _closeCommand = new RelayCommand(a => _host.Close());
                }

                return _closeCommand;
            }
        }

        /// <summary>
        /// Put the report on the clipboard as a Markdown text.
        /// </summary>
        public ICommand CopyCommand
        {
            get
            {
                if (_copyCommand == null)
                {
                    _copyCommand = new RelayCommand(a => CopyAsMarkdown());
                }

                return _copyCommand;
            }
        }

        /// <summary>
        /// Save the report to a file (Markdown, or JSON by the extension).
        /// </summary>
        public ICommand SaveCommand
        {
            get
            {
                if (_saveCommand == null)
                {
                    _saveCommand = new RelayCommand(a => Save());
                }

                return _saveCommand;
            }
        }

        /// <summary>
        /// Open the log of the extension (it exists in a Debug build only).
        /// </summary>
        public ICommand OpenLogCommand
        {
            get
            {
                if (_openLogCommand == null)
                {
                    _openLogCommand = new AsyncRelayCommand(
                        async a => await FileNavigator.OpenAsync(LogPath, 0),
                        a => File.Exists(LogPath)
                        );
                }

                return _openLogCommand;
            }
        }

        /// <summary>
        /// Open the file of a node (a double click).
        /// </summary>
        public ICommand OpenNodeCommand
        {
            get
            {
                if (_openNodeCommand == null)
                {
                    _openNodeCommand = new AsyncRelayCommand(
                        async a =>
                        {
                            if (a is ReportNodeViewModel node && node.FilePath != null && File.Exists(node.FilePath))
                            {
                                await FileNavigator.OpenAsync(node.FilePath, node.Line);
                            }
                        });
                }

                return _openNodeCommand;
            }
        }

        private static string LogPath => Path.Combine(Path.GetTempPath(), "AdjustNamespace.vs.log");

        /// <param name="host">The window this step is shown in.</param>
        /// <param name="parameters">Parameters of this step.</param>
        public ReportStepViewModel(
            IWizardHost host,
            ReportParameters parameters
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            _host = host;
            _report = parameters.Report;
            _rolledBack = parameters.RolledBack;

            Sections = new ObservableCollection<ReportNodeViewModel>(
                AdjustReportSections.Build(_report).Select(i => new ReportNodeViewModel(i))
                );
        }

        /// <inheritdoc/>
        public override System.Threading.Tasks.Task StartAsync()
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        private void CopyAsMarkdown()
        {
            try
            {
                Clipboard.SetText(_report.ToMarkdown());
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                //the clipboard is busy: the user presses the button once more
            }
        }

        private void Save()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save the report",
                FileName = "adjust-namespaces-report",
                DefaultExt = ".md",
                Filter = "Markdown (*.md)|*.md|JSON (*.json)|*.json",
                AddExtension = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var isJson = string.Equals(Path.GetExtension(dialog.FileName), ".json", StringComparison.OrdinalIgnoreCase);

            try
            {
                File.WriteAllText(
                    dialog.FileName,
                    isJson ? _report.ToJson() : _report.ToMarkdown(),
                    new System.Text.UTF8Encoding(false)
                    );
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                System.Windows.MessageBox.Show(exception.Message, "Adjust Namespaces", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
