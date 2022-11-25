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
            _isInProgress = true;
            OnPropertyChanged();

            await TaskScheduler.Default;

            #region check for solution compilation

            foreach (var project in _vss.Workspace.CurrentSolution.Projects)
            {
                MainMessage = $"Processing {project.Name}";

                var compilation = await project.GetCompilationAsync();
                if (compilation != null)
                {
                    if (compilation.GetDiagnostics().Any(j => j.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
                    {
                        await AddMessageAsync(
                            $"Compilation of {project.Name} fails. Adjust namespace can produce an incorrect results."
                            );
                    }
                }
            }

            #endregion

            //extract project items (we need perform this in main thread)
            var fileExtensions = await FillFileExtensionAsync();

            //the below we may run into background thread
            //await TaskScheduler.Default;

            #region check for the target namespace already contains a type with same name

            _filteredFileExs = new List<FileEx>();

            foreach (var fileExtension in fileExtensions)
            {
                var subjectFilePath = fileExtension.FilePath;
                var subjectProject = fileExtension.Project;
                var subjectProjectItem = fileExtension.ProjectItem;

                MainMessage = $"Processing {subjectFilePath}";

                var roslynProject = _vss.Workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == subjectProjectItem!.ContainingProject.FullName);
                if (roslynProject == null)
                {
                    continue;
                }

                if (subjectFilePath.EndsWith(".xaml"))
                {
                    //we want to process XAML documents! no need for additional checks
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

                var targetNamespace = await roslynProject.TryDetermineTargetNamespaceAsync(subjectFilePath, _vss);
                if (string.IsNullOrEmpty(targetNamespace))
                {
                    continue;
                }

                var ntc = NamespaceTransitionContainer.GetNamespaceTransitionsFor(subjectSyntaxRoot, targetNamespace!);
                if (ntc.IsEmpty)
                {
                    continue;
                }

                // get all types in the target namespace
                var typesInTargetNamespace = await TypeContainer.CreateForAsync(
                    _vss.Workspace,
                    new[] { targetNamespace! }
                    );

                //check for same types already exists in the destination namespace
                foreach (var foundType in subjectSyntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var symbolInfo = subjectSemanticModel.GetDeclaredSymbol(foundType);
                    if (symbolInfo == null)
                    {
                        continue;
                    }

                    var targetNamespaceInfo = ntc.TransitionDict[symbolInfo.ContainingNamespace.ToDisplayString()];

                    if (typesInTargetNamespace.ContainsType($"{targetNamespaceInfo}.{symbolInfo.Name}"))
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

        private async Task<List<FileExtension>> FillFileExtensionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var fileExtensions = new List<FileExtension>(_filePaths.Count);
            foreach (var filePath in _filePaths)
            {
                if (!_vss.Dte.Solution.TryGetProjectItem(filePath, out var subjectProject, out var subjectProjectItem))
                {
                    continue;
                }

                fileExtensions.Add(new FileExtension(filePath, subjectProject!, subjectProjectItem!));
            }

            return fileExtensions;
        }

        private async System.Threading.Tasks.Task AddMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DetectedMessages.Add(message);

            //await TaskScheduler.Default;
        }


        /// <summary>
        /// Extension for file from workspace.
        /// </summary>
        private readonly struct FileExtension
        {
            public readonly string FilePath;
            public readonly EnvDTE.Project Project;
            public readonly string ProjectPath;
            public readonly ProjectItem ProjectItem;

            public FileExtension(
                string filePath,
                EnvDTE.Project project,
                ProjectItem projectItem
                )
            {
                Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

                if (filePath is null)
                {
                    throw new ArgumentNullException(nameof(filePath));
                }

                FilePath = filePath;
                Project = project;
                ProjectPath = project.FullName;
                ProjectItem = projectItem;
            }

        }

    }
}
