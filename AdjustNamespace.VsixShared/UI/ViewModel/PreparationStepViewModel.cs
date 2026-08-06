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
    /// <summary>
    /// Viewmodel of the first wizard step.
    /// It compiles every project of the solution and reports the compilation errors:
    /// the adjusting is based on the semantic model, so a broken solution may lead
    /// to the incorrect results. The user is allowed to move next anyway.
    /// </summary>
    public class PreparationStepViewModel : ChainViewModel
    {
        private readonly AdjustContext _context;
        private readonly IWizardHost _host;
        private readonly IStepFactory<SelectedStepParameters> _nextStepFactory;
        private readonly HashSet<string> _filePaths;

        private string _mainMessage;
        private bool _isInProgress = false;
        private bool _blocked = false;

        private ICommand? _closeCommand;
        private ICommand? _repeatCommand;
        private ICommand? _nextCommand;

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
        /// Found compilation problems.
        /// </summary>
        public ObservableCollection<string> DetectedMessages
        {
            get;
            private set;
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
                            _host.Close();
                        },
                        r => !_isInProgress
                        );
                }

                return _closeCommand;
            }
        }

        /// <summary>
        /// Repeat the compilation check (available if a problem has been found).
        /// </summary>
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

        /// <summary>
        /// Go to the next wizard step.
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

        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="host">The window this step is shown in.</param>
        /// <param name="nextStepFactory">Factory of the next wizard step.</param>
        /// <param name="parameters">Parameters of this step.</param>
        public PreparationStepViewModel(
            AdjustContext context,
            IWizardHost host,
            IStepFactory<SelectedStepParameters> nextStepFactory,
            PreparationParameters parameters
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            _context = context;
            _host = host;
            _nextStepFactory = nextStepFactory;
            _filePaths = parameters.FilePaths;

            _mainMessage = "Scanning solution...";

            DetectedMessages = new ObservableCollection<string>();
        }

        /// <inheritdoc/>
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

        /// <summary>
        /// Compile every project of the solution and collect the found errors.
        /// </summary>
        /// <exception cref="CompilationException">A project cannot be compiled at all.</exception>
        private async Task CheckForSolutionCompilationAsync()
        {
            var errorFound = false;

            var index = 1;
            var total = _context.Workspace.CurrentSolution.Projects.Count();
            foreach (var project in _context.Workspace.CurrentSolution.Projects)
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

        /// <summary>
        /// Add a message into <see cref="DetectedMessages"/> (from the main thread).
        /// </summary>
        private async System.Threading.Tasks.Task AddMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DetectedMessages.Add(message);
        }

        /// <summary>
        /// A project of the solution cannot be compiled.
        /// </summary>
        private sealed class CompilationException : Exception
        {
            /// <summary>
            /// Name of the problem project.
            /// </summary>
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
