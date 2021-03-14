using AdjustNamespace.Helper;
using AdjustNamespace.Mover;
using EnvDTE80;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Threading;

namespace AdjustNamespace.ViewModel
{
    public class PreparationStepViewModel : ChainViewModel
    {
        private readonly IChainMoverState _moverState;
        private readonly List<string> _filePaths;

        private string _detectedMessages;
        private Brush _foreground;
        private string _mainMessage;

        public string MainMessage
        {
            get => _mainMessage;
            private set
            {
                _mainMessage = value;
                OnPropertyChanged(nameof(MainMessage));
            }
        }

        public string DetectedMessages
        {
            get => _detectedMessages;
            private set
            {
                _detectedMessages = value;
                OnPropertyChanged(nameof(DetectedMessages));
            }
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

        public PreparationStepViewModel(
            IChainMoverState moverState,
            Dispatcher dispatcher,
            List<string> filePaths
            )
            : base(dispatcher)
        {
            if (moverState is null)
            {
                throw new ArgumentNullException(nameof(moverState));
            }

            if (filePaths is null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }
            _moverState = moverState;
            _filePaths = filePaths;
            _detectedMessages = string.Empty;
            _foreground = Brushes.Green;
            _mainMessage = "Scanning solution...";
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            var dte = await _moverState.ServiceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as DTE2;
            if (dte == null)
            {
                return;
            }

            var componentModel = (IComponentModel)await _moverState.ServiceProvider.GetServiceAsync(typeof(SComponentModel));
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
                        DetectedMessages += Environment.NewLine + $"Compilation of {project.Name} fails. Adjust namespace can produce an incorrect results.";
                    }
                }
            }

            #endregion

            #region check for the target namespace already contains a type with same name

            foreach (var subjectFilePath in _filePaths)
            {
                MainMessage = $"Processing {subjectFilePath}";

                var subjectProjectItem = dte.Solution.TryGetProjectItem(subjectFilePath);
                if (subjectProjectItem == null)
                {
                    continue;
                }

                var roslynProject = workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == subjectProjectItem.ContainingProject.FullName);
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

                var targetNamespace = roslynProject.GetTargetNamespace(subjectFilePath);

                var namespaceInfos = subjectSyntaxRoot.GetAllNamespaceInfos(targetNamespace);
                if (namespaceInfos.Count == 0)
                {
                    continue;
                }

                var namespaceRenameDict = namespaceInfos.BuildRenameDict();

                // get all types in the target namespace
                var foundTypesInTargetNamespace = await workspace.GetAllTypesInNamespaceRecursivelyAsync(new[] { targetNamespace });

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
                        DetectedMessages += Environment.NewLine + $"'{targetNamespace}' already contains a type '{symbolInfo.Name}'";
                        _moverState.BlockMovingForward = true;
                        return;
                    }
                }
            }

            #endregion

            MainMessage = $"Let's move next!";
        }
    }
}
