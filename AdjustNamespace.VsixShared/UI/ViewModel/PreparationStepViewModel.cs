using AdjustNamespace.Helper;
using EnvDTE80;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AdjustNamespace.UI.StepFactory;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Microsoft.CodeAnalysis;
using EnvDTE;
using AdjustNamespace.Namespace;
using Community.VisualStudio.Toolkit;
using System.Runtime.Serialization;
using System.Diagnostics;

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

        private List<FileEx>? _filteredFileExs = null;

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
                            if (_filteredFileExs != null)
                            {
                                await _nextStepFactory.CreateAsync(_filteredFileExs);
                            }
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
            catch (FileProcessException excp)
            {
                await AddMessageAsync(
                    $"Processing {excp.FilePath} fails."
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

            DetectedMessages.Clear();

            OnPropertyChanged();

            await TaskScheduler.Default;

            await CheckForSolutionCompilationAsync();

            //extract project items (we need perform this in main thread)
            var fileExtensions = await FillFileExtensionAsync();

            //the below we may run into background thread
            //await TaskScheduler.Default;

            #region check for the target namespace already contains a type with same name

            _filteredFileExs = new List<FileEx>();

            // get all types in solution
            var typesInSolutionPerNamespace = await NamespaceTypeContainer.CreateForAsync(
                _vss.Workspace
                );

            var total = fileExtensions.Count;
            for (int i = 0; i < total; i++)
            {
                FileExtension fileExtension = fileExtensions[i];
                var subjectFilePath = fileExtension.FilePath;
                var subjectProject = fileExtension.Project;
                var subjectProjectItem = fileExtension.ProjectItem;

                MainMessage = $"{i+1}/{total} Processing {subjectFilePath}";

                if (subjectFilePath.EndsWith(".xaml"))
                {
                    //we want to process XAML documents!
                    //TODO: need for additional checks
                    _filteredFileExs.Add(
                        new FileEx(fileExtension.FilePath, fileExtension.ProjectPath)
                        );
                    continue;
                }

                var subjectDocument = _vss.Workspace.GetDocument(subjectFilePath);
                if (!subjectDocument.IsDocumentInScope())
                {
                    continue;
                }

                var subjectSemanticModel = await subjectDocument!.GetSemanticModelAsync();
                if (subjectSemanticModel == null)
                {
                    continue;
                }

                var subjectSyntaxRoot = await subjectDocument.GetSyntaxRootAsync();
                if (subjectSyntaxRoot == null)
                {
                    continue;
                }

                var targetNamespace = await NamespaceHelper.TryDetermineTargetNamespaceAsync(subjectProject, subjectFilePath, _vss);
                if (string.IsNullOrEmpty(targetNamespace))
                {
                    continue;
                }

                var ntc = NamespaceTransitionContainer.GetNamespaceTransitionsFor(subjectSyntaxRoot, targetNamespace!);
                if (ntc.IsEmpty)
                {
                    continue;
                }

                //check for same types already exists in the destination namespace
                foreach (var foundType in subjectSyntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var symbolInfo = subjectSemanticModel.GetDeclaredSymbol(foundType);
                    if (symbolInfo == null)
                    {
                        continue;
                    }

                    var symbolNamespace = symbolInfo.ContainingNamespace.ToDisplayString();
                    if(symbolNamespace == targetNamespace)
                    {
                        continue;
                    }

                    if(NamespaceHelper.IsSpecialNamespace(symbolNamespace))
                    {
                        continue;
                    }

                    var targetNamespaceInfo = ntc.TransitionDict[symbolNamespace];
                    if (typesInSolutionPerNamespace.CheckForTypeExists(targetNamespaceInfo.ModifiedName, symbolInfo.Name))
                    {
                        await AddMessageAsync(
                            $"'{targetNamespace}' already contains a type '{symbolInfo.Name}'"
                            );

                        _blocked = true;
                        _filteredFileExs.Clear();
                        return;
                    }
                }

                _filteredFileExs.Add(
                    new FileEx(fileExtension.FilePath, fileExtension.ProjectPath)
                    );
            }

            #endregion

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            MainMessage = $"Let's move next!";
            _isInProgress = false;

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

        private async Task<List<FileExtension>> FillFileExtensionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var fileExtensions = new List<FileExtension>(_filePaths.Count);

            var projectItems = await SolutionHelper.GetAllProjectItemsAsync(null);
            foreach (var filePath in _filePaths)
            {
                try
                {
                    if(!projectItems.TryGetValue(filePath, out var pii))
                    {
                        continue;
                    }

                    fileExtensions.Add(new FileExtension(filePath, pii.Project, pii.ProjectItem));
                }
                catch (Exception ex)
                {
                    throw new FileProcessException(filePath, ex);
                }
            }

            return fileExtensions;
        }

        private async System.Threading.Tasks.Task AddMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DetectedMessages.Add(message);

            //await TaskScheduler.Default;
        }

        private sealed class FileProcessException : Exception
        {
            public string FilePath { get; }

            public FileProcessException(
                string filePath,
                Exception ex
                )
                : base($"Processing of {filePath} failed", ex)
            {
                FilePath = filePath;
            }
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

        /// <summary>
        /// Extension for file from workspace.
        /// </summary>
        [DebuggerDisplay("{FilePath}")]
        private readonly struct FileExtension
        {
            public readonly string FilePath;
            public readonly SolutionItem Project;
            public readonly string ProjectPath;
            public readonly SolutionItem ProjectItem;

            public FileExtension(
                string filePath,
                SolutionItem project,
                SolutionItem projectItem
                )
            {
                if (project is null)
                {
                    throw new ArgumentNullException(nameof(project));
                }

                if (projectItem is null)
                {
                    throw new ArgumentNullException(nameof(projectItem));
                }
                if (filePath is null)
                {
                    throw new ArgumentNullException(nameof(filePath));
                }

                FilePath = filePath;
                Project = project;
                ProjectPath = project.FullPath!;
                ProjectItem = projectItem;
            }

        }

    }
}
