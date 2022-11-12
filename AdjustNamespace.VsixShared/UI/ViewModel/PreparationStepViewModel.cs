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

namespace AdjustNamespace.UI.ViewModel
{
    public class PreparationStepViewModel : ChainViewModel
    {
        private readonly IAsyncServiceProvider _serviceProvider;
        private readonly IStepFactory _nextStepFactory;
        private readonly List<string> _filePaths;

        private string _mainMessage;
        private bool _isInProgress = false;
        private bool _blocked = false;

        private ICommand? _closeCommand;
        private ICommand? _nextCommand;

        private List<FileExtension>? _fileExtensions = null;

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
                            if (_fileExtensions != null)
                            {
                                await _nextStepFactory.CreateAsync(_fileExtensions);
                            }
                        },
                        r => !_blocked && !_isInProgress
                        );
                }

                return _nextCommand;
            }
        }

        public PreparationStepViewModel(
            IAsyncServiceProvider serviceProvider,
            IStepFactory nextStepFactory,
            List<string> filePaths
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            if (filePaths is null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }
            _serviceProvider = serviceProvider;
            _nextStepFactory = nextStepFactory;
            _filePaths = filePaths;
            _mainMessage = "Scanning solution...";

            DetectedMessages = new ObservableCollection<string>();
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            _isInProgress = true;
            OnPropertyChanged();

            var dte = await _serviceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as DTE2;
            if (dte == null)
            {
                return;
            }

            var componentModel = (await _serviceProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel)!;
            if (componentModel == null)
            {
                return;
            }

            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null)
            {
                return;
            }

            await TaskScheduler.Default;

            #region check for solution compilation

            foreach (var project in workspace.CurrentSolution.Projects)
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
            await FillFileExtensionAsync(dte);

            //the below we may run into background thread
            //await TaskScheduler.Default;

            #region check for the target namespace already contains a type with same name

            foreach (var fileExtension in _fileExtensions!)
            {
                var subjectFilePath = fileExtension.FilePath;
                var subjectProject = fileExtension.Project;
                var subjectProjectItem = fileExtension.ProjectItem;

                MainMessage = $"Processing {subjectFilePath}";

                var roslynProject = workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == subjectProjectItem!.ContainingProject.FullName);
                if (roslynProject == null)
                {
                    continue;
                }

                var subjectDocument = workspace.GetDocument(subjectFilePath);
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

                if (!roslynProject.TryGetTargetNamespace(subjectFilePath, out var targetNamespace))
                {
                    continue;
                }

                var namespaceInfos = subjectSyntaxRoot.GetAllNamespaceInfos(targetNamespace!);
                if (namespaceInfos.Count == 0)
                {
                    continue;
                }

                var namespaceRenameDict = namespaceInfos.BuildRenameDict();

                // get all types in the target namespace
                var typesInTargetNamespace = await TypeContainer.CreateForAsync(
                    workspace,
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

                    var targetNamespaceInfo = namespaceRenameDict[symbolInfo.ContainingNamespace.ToDisplayString()];

                    if (typesInTargetNamespace.ContainsType($"{targetNamespaceInfo}.{symbolInfo.Name}"))
                    {
                        await AddMessageAsync(
                            $"'{targetNamespace}' already contains a type '{symbolInfo.Name}'"
                            );
                        _blocked = true;
                        return;
                    }
                }
            }

            #endregion

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            MainMessage = $"Let's move next!";
            _isInProgress = false;

            OnPropertyChanged();
        }

        private async Task FillFileExtensionAsync(DTE2 dte)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _fileExtensions = new List<FileExtension>();
            foreach (var filePath in _filePaths)
            {
                if (!dte.Solution.TryGetProjectItem(filePath, out var subjectProject, out var subjectProjectItem))
                {
                    continue;
                }

                _fileExtensions.Add(new FileExtension(filePath, subjectProject!, subjectProjectItem!));
            }
        }

        private async Task AddMessageAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DetectedMessages.Add(message);

            await TaskScheduler.Default;
        }
    }
}
