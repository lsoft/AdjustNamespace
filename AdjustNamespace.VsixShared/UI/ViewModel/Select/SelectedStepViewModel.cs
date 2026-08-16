using AdjustNamespace.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using AdjustNamespace.UI.StepFactory;
using Microsoft.CodeAnalysis;
using AdjustNamespace.Adjusting;
using AdjustNamespace.Adjusting.Plan;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace AdjustNamespace.UI.ViewModel.Select
{
    /// <summary>
    /// Viewmodel of the second wizard step.
    /// It scans the chosen files (see <see cref="SubjectFileCollector"/>), shows the files
    /// which are really the subject to change (grouped by their physical folder) and allows
    /// the user to uncheck some of them, to tune the target namespace with a regex and
    /// to decide whether the changed files should be opened in the editor.
    /// Files which cannot be adjusted are listed separately with a reason.
    /// </summary>
    public class SelectedStepViewModel : ChainViewModel
    {
        /// <summary>
        /// The maximum number of files that can be opened in the editor without causing a delay in VS.
        /// </summary>
        public const int MaxFilesAllowedToOpen = 15;

        private readonly AdjustContext _context;
        private readonly IWizardHost _host;
        private readonly IStepFactory<PreparationParameters> _previousStepFactory;
        private readonly IStepFactory<PerformingParameters> _nextStepFactory;
        private readonly HashSet<string> _filePaths;

        /// <summary>
        /// The scan has been finished successfully, so the user is allowed to move next
        /// when at least one adjustable file is checked.
        /// </summary>
        private bool _statusOk = false;

        private Brush _foreground;
        private string _mainMessage = string.Empty;

        private ICommand? _closeCommand;
        private ICommand? _nextCommand;
        private ICommand? _invertStatusCommand;
        private ICommand? _previousCommand;
        private ICommand? _rescanCommand;
        
        private bool _enableOpenFileCheckBox;
        private bool _openFilesToEnableUndo;
        private string _replaceRegex = string.Empty;
        private string _replacedString = string.Empty;

        /// <summary>
        /// Status line of the step.
        /// </summary>
        public string MainMessage
        {
            get => _mainMessage;
            private set
            {
                _mainMessage = value;
                OnPropertyChanged(nameof(MainMessage));
            }
        }

        /// <summary>
        /// The flat list shown to the user: a folder followed by its files, then the next folder etc.
        /// </summary>
        public ObservableCollection<ISelectItemViewModel> ToFilterItems
        {
            get;
            private set;
        }

        /// <summary>
        /// Files which cannot be adjusted, with a reason.
        /// </summary>
        public ObservableCollection<BlockedFileViewModel> BlockedItems
        {
            get;
            private set;
        }

        /// <summary>
        /// There is at least one blocked file to show.
        /// </summary>
        public bool HasBlockedItems => BlockedItems.Count > 0;

        /// <summary>
        /// Color of the status line.
        /// </summary>
        public Brush Foreground
        {
            get => _foreground;
            private set
            {
                _foreground = value;
                OnPropertyChanged(nameof(Foreground));
            }
        }

        #region open files to enable undo checkbox

        /// <summary>
        /// The `open affected files` checkbox is available
        /// (i.e. not too many files are checked, see <see cref="MaxFilesAllowedToOpen"/>).
        /// </summary>
        public bool EnableOpenFileCheckBox
        {
            get => _enableOpenFileCheckBox;
            set
            {
                _enableOpenFileCheckBox = value;
                OnPropertyChanged(nameof(EnableOpenFileCheckBox));
            }
        }

        /// <summary>
        /// Text of the `open affected files` checkbox.
        /// </summary>
        public string OpenFileCheckBoxText => $"Open affected files to enable Undo (max is {MaxFilesAllowedToOpen} to prevent delays)";

        /// <summary>
        /// Open the changed files in the editor (this allows the user to undo the changes).
        /// </summary>
        public bool OpenFilesToEnableUndo
        {
            get => _openFilesToEnableUndo;
            set
            {
                _openFilesToEnableUndo = value;
                OnPropertyChanged(nameof(OpenFilesToEnableUndo));
            }
        }

        #endregion

        /// <summary>
        /// The regex which additionally modifies the target namespace.
        /// Changing it invalidates the scan results, so the user has to rescan.
        /// </summary>
        public string ReplaceRegex
        {
            get => _replaceRegex;
            set
            {
                _replaceRegex = value;

                Reset();

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// The replacement for the fragment found by <see cref="ReplaceRegex"/>.
        /// Changing it invalidates the scan results, so the user has to rescan.
        /// </summary>
        public string ReplacedString
        {
            get => _replacedString;
            set
            {
                _replacedString = value;

                Reset();

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Built in regex samples the user may apply in one click.
        /// </summary>
        public ObservableCollection<KnownRegex> KnownRegexes
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
                    _closeCommand = new RelayCommand(
                        a =>
                        {
                            Cleanup();

                            _host.Close();
                        }
                        );
                }

                return _closeCommand;
            }
        }

        /// <summary>
        /// Go back to the previous wizard step.
        /// </summary>
        public ICommand PreviousCommand
        {
            get
            {
                if (_previousCommand == null)
                {
                    _previousCommand = new AsyncRelayCommand(
                        async a =>
                        {
                            await _previousStepFactory.CreateAsync(
                                new PreparationParameters(_filePaths)
                                );
                        }
                        );
                }

                return _previousCommand;
            }
        }

        /// <summary>
        /// Rescan the chosen files (required after the regex has been changed).
        /// </summary>
        public ICommand RescanCommand
        {
            get
            {
                if (_rescanCommand == null)
                {
                    _rescanCommand = new AsyncRelayCommand(
                        async a =>
                        {
                            await RescanAsync();
                        },
                        r => !_statusOk
                        );
                }

                return _rescanCommand;
            }
        }

        /// <summary>
        /// Go to the last wizard step, i.e. start the adjusting of the checked files.
        /// </summary>
        public ICommand NextCommand
        {
            get
            {
                if (_nextCommand == null)
                {
                    _nextCommand = new AsyncRelayCommand(
                        async a =>
                        {
                            var filePaths = ToFilterItems
                                .Where(s => s.FileEx.HasValue && s.IsChecked.GetValueOrDefault(false))
                                .Select(s => s.FileEx!.Value.FilePath)
                                .ToList();
                            var pp = new PerformingParameters(
                                filePaths,
                                CreateNamespaceReplaceRegex(),
                                _openFilesToEnableUndo
                                );

                            Cleanup();

                            await _nextStepFactory.CreateAsync(pp);
                        },
                        r => _statusOk && ToFilterItems.Any(s => s.IsChecked.GetValueOrDefault(false))
                        );
                }

                return _nextCommand;
            }
        }

        /// <summary>
        /// Invert the checkbox of the selected items (bound to the Space key).
        /// </summary>
        public ICommand InvertStatusCommand
        {
            get
            {
                if (_invertStatusCommand == null)
                {
                    _invertStatusCommand = new RelayCommand(
                        a =>
                        {
                            var selected = ToFilterItems.Where(i => i.IsSelected).ToList();

                            if (selected.Count == 0)
                            {
                                return;
                            }

                            var newValue = !selected[0].IsChecked.GetValueOrDefault(false);
                            selected.ForEach(s => s.IsChecked = newValue);
                        },
                        a => _statusOk
                        );
                }

                return _invertStatusCommand;
            }
        }


        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="host">The window this step is shown in.</param>
        /// <param name="previousStepFactory">Factory of the previous wizard step.</param>
        /// <param name="nextStepFactory">Factory of the next wizard step.</param>
        /// <param name="parameters">Parameters of this step.</param>
        public SelectedStepViewModel(
            AdjustContext context,
            IWizardHost host,
            IStepFactory<PreparationParameters> previousStepFactory,
            IStepFactory<PerformingParameters> nextStepFactory,
            SelectedStepParameters parameters
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (previousStepFactory is null)
            {
                throw new ArgumentNullException(nameof(previousStepFactory));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            _context = context;
            _host = host;
            _previousStepFactory = previousStepFactory;
            _nextStepFactory = nextStepFactory;
            _filePaths = parameters.FilePaths;

            _foreground = Brushes.Green;
            ToFilterItems = new ObservableCollection<ISelectItemViewModel>();
            BlockedItems = new ObservableCollection<BlockedFileViewModel>();

            KnownRegexes = new ObservableCollection<KnownRegex>(
                [
                    new KnownRegex(
                        "Any.Name.Space -> NewNamespace",
                        ".+",
                        string.Empty,
                        a =>
                        {
                            ReplaceRegex = a.ReplaceRegex;
                            ReplacedString = a.ReplacedString;
                        }
                        ),
                    new KnownRegex(
                        "First.Part.Of.The.Name -> NewNamespace.Part.Of.The.Name",
                        "^[^.]+",
                        string.Empty,
                        a =>
                        {
                            ReplaceRegex = a.ReplaceRegex;
                            ReplacedString = a.ReplacedString;
                        }
                        ),
                    new KnownRegex(
                        "Last.Part.Of.The.Name -> Last.Part.Of.The.NewNamespace",
                        "[^.]+$",
                        string.Empty,
                        a =>
                        {
                            ReplaceRegex = a.ReplaceRegex;
                            ReplacedString = a.ReplacedString;
                        }
                        ),


                ]);
        }

        /// <inheritdoc/>
        public override async System.Threading.Tasks.Task StartAsync()
        {
            await RescanAsync();
        }

        /// <summary>
        /// Drop the scan results (the user has changed the target namespace regex).
        /// </summary>
        private void Reset()
        {
            MainMessage = "Target namespace regex changed. Press Rescan button.";

            _statusOk = false;

            ToFilterItems.Clear();
            BlockedItems.Clear();
            OnPropertyChanged(nameof(HasBlockedItems));
        }

        /// <summary>
        /// Scan the chosen files and rebuild the list shown to the user.
        /// </summary>
        private async Task RescanAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                MainMessage = "Scanning for files to adjust...";

                var replaceRegex = CreateNamespaceReplaceRegex();

                var sfc = new SubjectFileCollector(
                    _context,
                    _filePaths,
                    replaceRegex
                    );
                var sr = await sfc.AnalyzeAndCollectAsync(
                    (progress, total, filePaths) =>
                    {
                        MainMessage = $"{progress}/{total} Processing {filePaths}";
                    }
                    );

                BuildTree(sr.CollectedFiles);
                BuildBlocked(sr.Blocked);

                MainMessage = BuildScanSummary(sr.CollectedFiles.Count, sr.Blocked.Count);

                _statusOk = true;
            }
            catch (FileProcessException excp)
            {
                MainMessage =
                    $"Processing {excp.FilePath} fails."
                    + Environment.NewLine
                    + excp.Message
                    ;

                Logging.LogVS(excp);
            }
        }

        private static string BuildScanSummary(
            int collectedCount,
            int blockedCount
            )
        {
            if (blockedCount == 0)
            {
                return $"Total {collectedCount} file(s) found. Choose files to process...";
            }

            if (collectedCount == 0)
            {
                return $"{blockedCount} file(s) cannot be adjusted.";
            }

            return $"{collectedCount} file(s) to adjust, {blockedCount} cannot be adjusted.";
        }

        /// <summary>
        /// Build the target namespace modifier out of the values entered by the user.
        /// </summary>
        private NamespaceReplaceRegex CreateNamespaceReplaceRegex()
        {
            return new NamespaceReplaceRegex(ReplaceRegex, ReplacedString);
        }


        /// <summary>
        /// Build the flat `folder + its files` list out of the found files.
        /// </summary>
        private void BuildTree(
            List<FileEx> filteredFileExs
            )
        {
            ToFilterItems.Clear();

            //perform grouping by files physical folder!
            var dirPaths = filteredFileExs.Select(f => f.FolderPath).Distinct().ToList();
            var dirs = new List<SelectFolderViewModel>();
            foreach (var dirPath in dirPaths)
            {
                var dir = new SelectFolderViewModel(
                    this,
                    dirPath
                    );

                var dirFiles = filteredFileExs
                    .Where(f => f.FolderPath == dirPath)
                    .Select(f => new SelectFileViewModel(f, dir))
                    .ToList();
                dir.AddFiles(dirFiles);

                dirs.Add(dir);
            }

            foreach (var dir in dirs.OrderBy(d => d.ItemPath))
            {
                ToFilterItems.Add(dir);

                foreach (var file in dir.Files)
                {
                    ToFilterItems.Add(file);
                }
            }

            RefreshStatus();
            OnPropertyChanged();
        }

        /// <summary>
        /// Show the files which cannot be adjusted.
        /// </summary>
        private void BuildBlocked(
            IReadOnlyList<AdjustBlock> blocked
            )
        {
            BlockedItems.Clear();

            foreach (var block in blocked.OrderBy(b => b.FilePath))
            {
                BlockedItems.Add(new BlockedFileViewModel(block));
            }

            OnPropertyChanged(nameof(HasBlockedItems));
        }

        /// <summary>
        /// Re-evaluate the state of the step after a checkbox has been changed.
        /// Called by the child viewmodels.
        /// </summary>
        public void RefreshStatus()
        {
            RefreshOpenFileCheckBox();
        }

        /// <summary>
        /// Disable (and uncheck) the `open affected files` checkbox if too many files are checked:
        /// a lot of documents opened at once makes Visual Studio unresponsive.
        /// </summary>
        private void RefreshOpenFileCheckBox()
        {
            var cnt = ToFilterItems
                .Where(i => i.FileEx.HasValue && i.IsChecked.GetValueOrDefault(false))
                .Count();
            var allowed = cnt < MaxFilesAllowedToOpen;

            EnableOpenFileCheckBox = allowed;
            if (!allowed)
            {
                OpenFilesToEnableUndo = false;
            }
        }

        /// <summary>
        /// Clear the circular references to avoid possible difficulties for GC to grab that objects.
        /// </summary>
        private void Cleanup()
        {
            ToFilterItems.ForEach(i => i.Clear());
            ToFilterItems.Clear();
            BlockedItems.Clear();
        }
    }
}
