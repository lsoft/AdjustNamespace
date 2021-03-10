using AdjustNamespace.Helper;
using AdjustNamespace.Mover;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AdjustNamespace.ViewModel
{
    public class PerformingViewModel : ChainViewModel
    {
        private readonly IChainMoverState _moverState;
        private readonly List<string> _filePaths;

        private string _progressMessage;

        public string ProgressMessage
        {
            get => _progressMessage;
            private set
            {
                _progressMessage = value;
                OnPropertyChanged(nameof(ProgressMessage));
            }
        }

        public PerformingViewModel(
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
            _progressMessage = string.Empty;
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
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

            for(var i = 0; i < _filePaths.Count; i++)
            {
                ProgressMessage = $"{i} out of {_filePaths.Count}";

                var filePath = _filePaths[i];

                var subjectDocument = workspace.GetDocument(filePath);
                var subjectProject = subjectDocument!.Project;

                var projectFolderPath = new FileInfo(subjectProject.FilePath).Directory.FullName;
                var suffix = new FileInfo(filePath).Directory.FullName.Substring(projectFolderPath.Length);
                var targetNamespace = subjectProject.DefaultNamespace +
                    suffix
                        .Replace(Path.DirectorySeparatorChar, '.')
                        .Replace(Path.AltDirectorySeparatorChar, '.')
                        ;

                var subjectSemanticModel = await subjectDocument.GetSemanticModelAsync();
                if (subjectSemanticModel == null)
                {
                    continue;
                }

                var subjectSyntaxRoot = await subjectDocument.GetSyntaxRootAsync();
                if (subjectSyntaxRoot == null)
                {
                    continue;
                }

                var subjectNamespaces = subjectSyntaxRoot
                    .DescendantNodesAndSelf()
                    .OfType<NamespaceDeclarationSyntax>()
                    .ToList()
                    ;
                if (subjectNamespaces.Count == 0)
                {
                    continue;
                }

                var minimalDepth = subjectNamespaces.Min(n => n.GetDepth());

                var processedNamespaces = new List<NamespaceDeclarationSyntax>();
                foreach (var subjectNamespace in subjectNamespaces)
                {
                    if (minimalDepth == subjectNamespace.GetDepth())
                    {
                        processedNamespaces.Add(subjectNamespace);
                    }
                }


                var toDeleteNamespaces = new HashSet<string>();
                var editorProvider = new EditorProvider(workspace);

                #region fix refs (adding a new using namespace clauses)

                var toProcess = new Dictionary<string, HashSet<string>>();
                var processedTypes = new HashSet<string>();
                foreach (var processedNamespace in processedNamespaces)
                {
                    foreach (var foundType in processedNamespace.DescendantNodes().OfType<TypeDeclarationSyntax>())
                    {
                        var symbolInfo = subjectSemanticModel.GetDeclaredSymbol(foundType);
                        if (symbolInfo == null)
                        {
                            continue;
                        }

                        if (processedTypes.Contains(symbolInfo.ToDisplayString()))
                        {
                            //already processed
                            continue;
                        }

                        toDeleteNamespaces.Add(symbolInfo.ContainingNamespace.ToDisplayString());

                        var symbolTargetNamespace = symbolInfo.GetTargetNamespace(
                            processedNamespace.Name.ToString(),
                            targetNamespace
                            );

                        var foundReferences = await SymbolFinder.FindReferencesAsync(symbolInfo, workspace.CurrentSolution);
                        foreach (var foundReference in foundReferences)
                        {
                            foreach (var location in foundReference.Locations)
                            {
                                if (location.Document.FilePath == null)
                                {
                                    continue;
                                }

                                if (!toProcess.ContainsKey(location.Document.FilePath))
                                {
                                    toProcess[location.Document.FilePath] = new HashSet<string>();
                                }

                                toProcess[location.Document.FilePath].Add(symbolTargetNamespace);
                            }
                        }

                        processedTypes.Add(symbolInfo.ToDisplayString());
                    }
                }

                foreach (var group in toProcess)
                {
                    var targetFilePath = group.Key;

                    foreach (var symbolTargetNamespace in group.Value.OrderByDescending(a => a))
                    {
                        var documentEditor = await editorProvider.GetDocumentEditorAsync(targetFilePath);
                        if (documentEditor == null)
                        {
                            //skip this document
                            break;
                        }

                        var syntaxRoot = await documentEditor.OriginalDocument.GetSyntaxRootAsync();
                        if (syntaxRoot == null)
                        {
                            //skip this document
                            break;
                        }

                        var usingSyntaxes = syntaxRoot
                            .DescendantNodes()
                            .OfType<UsingDirectiveSyntax>()
                            .ToList();


                        if (usingSyntaxes.Count == 0)
                        {
                            //no namespaces exists
                            //no need to insert new in this file
                            break;
                        }
                        else
                        {
                            if (usingSyntaxes.Any(s => s.Name.ToString() == symbolTargetNamespace))
                            {
                                continue;
                            }

                            documentEditor.InsertAfter(
                                usingSyntaxes.Last(),
                                SyntaxFactory.UsingDirective(
                                    SyntaxFactory.ParseName(
                                        " " + symbolTargetNamespace
                                        )
                                    ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                );
                        }
                    }
                }

                #endregion

                #region fixed subject namespaces

                {
                    var subjectDocumentEditor = await editorProvider.GetDocumentEditorAsync(subjectDocument.FilePath!);

                    if (subjectDocumentEditor != null)
                    {
                        foreach (var processedNamespace in processedNamespaces)
                        {
                            var syntaxRoot = await subjectDocumentEditor.OriginalDocument
                                .GetSyntaxRootAsync()
                                ;
                            if (syntaxRoot == null)
                            {
                                continue;
                            }

                            var fNamespace = syntaxRoot.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault(mn => mn.Name == processedNamespace.Name);

                            var fixedNamespace = fNamespace.WithName(
                                SyntaxFactory.ParseName(
                                    targetNamespace
                                    ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                )
                                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                ;

                            subjectDocumentEditor.ReplaceNode(
                                fNamespace,
                                fixedNamespace
                                );
                        }
                    }

                    editorProvider.SaveAndClear();

                    //remove `using oldnamespace` if oldnamespace does not exists anymore
                    foreach (var document in workspace.EnumerateAllDocuments(Predicate.IsProjectInScope, Predicate.IsDocumentInScope))
                    {
                        var syntaxRoot = await document.GetSyntaxRootAsync();
                        if (syntaxRoot == null)
                        {
                            continue;
                        }

                        var usingSyntaxes = syntaxRoot
                            .DescendantNodes()
                            .OfType<UsingDirectiveSyntax>()
                            .ToList();

                        foreach (var usingSyntax in usingSyntaxes)
                        {
                            if (toDeleteNamespaces.Contains(usingSyntax.Name.ToString()))
                            {
                                var documentEditor = await editorProvider.GetDocumentEditorAsync(document.FilePath!);
                                if (documentEditor == null)
                                {
                                    continue;
                                }

                                documentEditor.RemoveNode(usingSyntax);
                            }
                        }
                    }
                }

                editorProvider.SaveAndClear();

                #endregion
            }

            ProgressMessage = $"Completed";
        }
    }
}
