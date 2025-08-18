using AdjustNamespace.Helper;
using Microsoft.VisualStudio.PlatformUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using AdjustNamespace.UI.StepFactory;
using Microsoft.CodeAnalysis;
using AdjustNamespace.UI.ViewModel.Select;
using AdjustNamespace.Adjusting;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace AdjustNamespace.UI.ViewModel
{
    public class SelectedStepViewModel : ChainViewModel
    {
        /// <summary>
        /// The maximum number of files that can be opened in the editor without causing a delay in VS.
        /// </summary>
        public const int MaxFilesAllowedToOpen = 15;

        private readonly VsServices _vss;
        private readonly IStepFactory _previousStepFactory;
        private readonly IStepFactory _nextStepFactory;
        private readonly HashSet<string> _filePaths;

        //private readonly List<FileEx> _filteredFileExs;

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

        public string MainMessage
        {
            get => _mainMessage;
            private set
            {
                _mainMessage = value;
                OnPropertyChanged(nameof(MainMessage));
            }
        }

        public ObservableCollection<ISelectItemViewModel> ToFilterItems
        {
            get;
            private set;
        }

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

        public bool EnableOpenFileCheckBox
        {
            get => _enableOpenFileCheckBox;
            set
            {
                _enableOpenFileCheckBox = value;
                OnPropertyChanged(nameof(EnableOpenFileCheckBox));
            }
        }

        public string OpenFileCheckBoxText => $"Open affected files to enable Undo (max is {MaxFilesAllowedToOpen} to prevent delays)";

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

        public ObservableCollection<KnownRegex> KnownRegexes
        {
            get;
        }

        public ICommand CloseCommand
        {
            get
            {
                if (_closeCommand == null)
                {
                    _closeCommand = new RelayCommand(
                        a =>
                        {
                            if (a is DialogWindow w)
                            {
                                Cleanup();
                                w.Close();
                            }
                        }
                        );
                }

                return _closeCommand;
            }
        }

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
                                _filePaths
                                );
                        }
                        );
                }

                return _previousCommand;
            }
        }

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


        public SelectedStepViewModel(
            VsServices vss,
            IStepFactory previousStepFactory,
            IStepFactory nextStepFactory,
            SelectedStepParameters parameters
            )
        {
            if (previousStepFactory is null)
            {
                throw new ArgumentNullException(nameof(previousStepFactory));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            _vss = vss;
            _previousStepFactory = previousStepFactory;
            _nextStepFactory = nextStepFactory;
            _filePaths = parameters.FilePaths;

            _foreground = Brushes.Green;
            ToFilterItems = new ObservableCollection<ISelectItemViewModel>();

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

        public override async System.Threading.Tasks.Task StartAsync()
        {
            await RescanAsync();
        }

        private void Reset()
        {
            MainMessage = "Target namespace regex changed. Press Rescan button.";

            _statusOk = false;

            ToFilterItems.Clear();
        }

        private async Task RescanAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                MainMessage = $"Scanning for a type name conflicts...";

                var replaceRegex = CreateNamespaceReplaceRegex();

                var sfc = new SubjectFileCollector(
                    _vss,
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

                MainMessage = $"Total {sr.CollectedFiles.Count} files found. Choose files to process...";

                _statusOk = true;
            }
            catch (FileProcessException excp)
            {
                MainMessage =
                    $"Processing {excp.FilePath} fails."
                    + Environment.NewLine
                    + $"Adjust namespace can produce an incorrect results."
                    + Environment.NewLine
                    + excp.Message
                    + Environment.NewLine
                    + excp.StackTrace
                    ;

                Logging.LogVS(excp);
            }
        }

        private NamespaceReplaceRegex CreateNamespaceReplaceRegex()
        {
            return new NamespaceReplaceRegex(ReplaceRegex, ReplacedString);
        }


        private void BuildTree(
            List<FileEx> filteredFileExs
            )
        {
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

        public void RefreshStatus()
        {
            RefreshOpenFileCheckBox();
        }

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
        }
    }
}
