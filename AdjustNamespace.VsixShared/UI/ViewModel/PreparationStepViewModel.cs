using AdjustNamespace.Helper;
using Microsoft.VisualStudio.PlatformUI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AdjustNamespace.UI.StepFactory;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Microsoft.CodeAnalysis;
using AdjustNamespace.Adjusting;

namespace AdjustNamespace.UI.ViewModel
{
    public class PreparationStepViewModel : ChainViewModel
    {
        private readonly VsServices _vss;
        private readonly IStepFactory _nextStepFactory;
        private readonly HashSet<string> _filePaths;

        private string _mainMessage;
        private bool _isInProgress = false;
        private bool _blocked = false;

        private ICommand? _closeCommand;
        private ICommand? _repeatCommand;
        private ICommand? _nextCommand;

        public string MainMessage
        {
            get => _mainMessage;
            private set
            {
                _mainMessage = value;
                OnPropertyChanged(nameof(MainMessage));
            }
        }

        public ObservableCollection<string> DetectedMessages
        {
            get;
            private set;
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
                                w.Close();
                            }
                        },
                        r => !_isInProgress
                        );
                }

                return _closeCommand;
            }
        }

        public ICommand RepeatCommand
        {
            get
            {
                if (_repeatCommand == null)
                {
                    _repeatCommand = new RelayCommand(
                        a =>
                        {
                            StartAsync().FileAndForget(nameof(RepeatCommand));
                        },
                        r => !_isInProgress && DetectedMessages.Count > 0
                        );
                }

                return _repeatCommand;
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
                            var parameters = new SelectedStepParameters(
                                _filePaths
                                );

                            await _nextStepFactory.CreateAsync(
                                parameters
                                );
                        },
                        r => !_blocked && !_isInProgress
                        );
                }

                return _nextCommand;
            }
        }

        public PreparationStepViewModel(
            VsServices vss,
            IStepFactory nextStepFactory,
            HashSet<string> filePaths
            )
        {
            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            if (filePaths is null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }

            _vss = vss;
            _nextStepFactory = nextStepFactory;
            _filePaths = filePaths;

            _mainMessage = "Scanning solution...";

            DetectedMessages = new ObservableCollection<string>();
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            try
            {
                await StartInternalAsync();
            }
            catch (CompilationException excp)
            {
                await AddMessageAsync(
                    $"Compilation {excp.Project} fails."
                    );
                await AddMessageAsync(
                    $"Adjust namespace can produce an incorrect results."
                    );
                await AddMessageAsync(
                    excp.Message
                    );
                await AddMessageAsync(
                    excp.StackTrace
                    );

                Logging.LogVS(excp);
            }
            //catch (FileProcessException excp)
            //{
            //    await AddMessageAsync(
            //        $"Processing {excp.FilePath} fails."
            //        );
            //    await AddMessageAsync(
            //        $"Adjust namespace can produce an incorrect results."
            //        );
            //    await AddMessageAsync(
            //        excp.Message
            //        );
            //    await AddMessageAsync(
            //        excp.StackTrace
            //        );

            //    Logging.LogVS(excp);
            //}
            catch (Exception excp)
            {
                await AddMessageAsync(
                    $"Compilation fails."
                    );
                await AddMessageAsync(
                    $"Adjust namespace can produce an incorrect results."
                    );
                await AddMessageAsync(
                    excp.Message
                    );
                await AddMessageAsync(
                    excp.StackTrace
                    );

                Logging.LogVS(excp);
            }
        }

        private async System.Threading.Tasks.Task StartInternalAsync()
        {
            _isInProgress = true;

            try
            {
                DetectedMessages.Clear();

                OnPropertyChanged();

                await TaskScheduler.Default;

                await CheckForSolutionCompilationAsync();

                //#region collect files which are subject to change    !!!AND!!!    check for the target namespace already contains a type with same name

                //_filteredFileExs.Clear();

                //var foundFileExs = await ScanForSubjectFilesAsync();
                //_filteredFileExs.AddRange(foundFileExs);

                //#endregion

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }
            finally
            {
                MainMessage = $"Let's move next!";
                _isInProgress = false;
            }

            OnPropertyChanged();
        }

        private async Task CheckForSolutionCompilationAsync()
        {
            var errorFound = false;

            var index = 1;
            var total = _vss.Workspace.CurrentSolution.Projects.Count();
            foreach (var project in _vss.Workspace.CurrentSolution.Projects)
            {
                try
                {
                    MainMessage = $"{index++}/{total} Processing {project.Name}";

                    var compilation = await project.GetCompilationAsync();
                    if (compilation != null)
                    {
                        var errors = compilation.GetDiagnostics().FindAll(j => j.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
                        if (errors.Count > 0)
                        {
                            await AddMessageAsync(
                                $"Compilation of {project.Name} fails:"
                                );
                            await AddMessageAsync(
                                new string(' ', 8) + string.Join(Environment.NewLine, errors.Select(e => e.GetMessage()))
                                );
                            errorFound = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new CompilationException(project.Name, ex);
                }
            }

            if (errorFound)
            {
                await AddMessageAsync(
                    $"Adjust namespace can produce an incorrect results."
                    );
            }
        }

        private async System.Threading.Tasks.Task AddMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DetectedMessages.Add(message);
        }

        private sealed class CompilationException : Exception
        {
            public string? Project { get; }

            public CompilationException()
            {
            }

            public CompilationException(string project, Exception ex)
                : base($"Compilation of {project} failed.")
            {
                Project = project;
            }
        }

    }
}
