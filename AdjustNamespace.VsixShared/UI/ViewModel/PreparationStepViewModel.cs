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
using System.Windows.Media;
using AdjustNamespace.UI.StepFactory;

namespace AdjustNamespace.UI.ViewModel
{
    public class PreparationStepViewModel : ChainViewModel
    {
        private readonly IAsyncServiceProvider _serviceProvider;
        private readonly IStepFactory _nextStepFactory;
        private readonly List<string> _filePaths;

        private Brush _foreground;
        private string _mainMessage;
        private bool _isInProgress = false;
        private bool _blocked = false;

        private ICommand? _closeCommand;
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

        public Brush Foreground
        {
            get => _foreground;
            private set
            {
                _foreground = value;
                OnPropertyChanged(nameof(Foreground));
            }
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
                        async a => await _nextStepFactory.CreateAsync(_filePaths),
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
            _foreground = Brushes.Green;
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

            #region check for solution compilation

            //await System.Threading.Tasks.Task.Delay(5000);

            foreach (var project in workspace.CurrentSolution.Projects)
            {
                MainMessage = $"Processing {project.Name}";

                var compilation = await project.GetCompilationAsync();
                if (compilation != null)
                {
                    if (compilation.GetDiagnostics().Any(j => j.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
                    {
                        Foreground = Brushes.Red;
                        DetectedMessages.Add(
                            $"Compilation of {project.Name} fails. Adjust namespace can produce an incorrect results."
                            );
                    }
                }
            }

            #endregion

            #region check for the target namespace already contains a type with same name

            foreach (var subjectFilePath in _filePaths)
            {
                MainMessage = $"Processing {subjectFilePath}";

                if (!dte.Solution.TryGetProjectItem(subjectFilePath, out var subjectProject, out var subjectProjectItem))
                {
                    continue;
                }

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
                var foundTypesInTargetNamespace = await workspace.GetAllTypesInNamespaceRecursivelyAsync(
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

                    if (foundTypesInTargetNamespace.ContainsKey($"{targetNamespaceInfo}.{symbolInfo.Name}"))
                    {
                        Foreground = Brushes.Red;
                        DetectedMessages.Add(
                            $"'{targetNamespace}' already contains a type '{symbolInfo.Name}'"
                            );
                        _blocked = true;
                        return;
                    }
                }
            }

            #endregion

            MainMessage = $"Let's move next!";
            _isInProgress = false;
            OnPropertyChanged();
        }
    }
}
