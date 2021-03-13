using AdjustNamespace.Helper;
using AdjustNamespace.Mover;
using AdjustNamespace.Xaml;
using EnvDTE80;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;

namespace AdjustNamespace.ViewModel
{
    public class PerformingViewModel : ChainViewModel
    {
        private readonly IChainMoverState _moverState;
        private readonly List<string> _subjectFilePaths;

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
            List<string> subjectFilePaths
            )
            : base(dispatcher)
        {
            if (moverState is null)
            {
                throw new ArgumentNullException(nameof(moverState));
            }

            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            _moverState = moverState;
            _subjectFilePaths = subjectFilePaths;
            _progressMessage = string.Empty;
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

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

            #region get all interestring files in current solution

            //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var filePaths = dte.Solution.ProcessSolution();

            //await TaskScheduler.Default;

            #endregion

            for (var i = 0; i < _subjectFilePaths.Count; i++)
            {
                var subjectFilePath = _subjectFilePaths[i];

                ProgressMessage = $"File #{i + 1} out of #{_subjectFilePaths.Count}";

                var subjectProjectItem = dte.Solution.GetProjectItem(subjectFilePath);
                if (subjectProjectItem == null)
                {
                    continue;
                }

                var roslynProject = workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == subjectProjectItem.ContainingProject.FullName);
                if (roslynProject == null)
                {
                    continue;
                }

                var targetNamespace = roslynProject.GetTargetNamespace(subjectFilePath);


                if (subjectFilePath.EndsWith(".xaml"))
                {
                    //it's a xaml

                    var xamlEngine = new XamlEngine(subjectFilePath);

                    if (!xamlEngine.GetRootInfo(out var rootNamespace, out var rootName))
                    {
                        continue;
                    }

                    if (rootNamespace != targetNamespace)
                    {
                        continue;
                    }

                    xamlEngine.MoveObject(
                        rootNamespace!,
                        rootName!,
                        targetNamespace
                        );

                    xamlEngine.SaveIfChangesExists();

                    continue;
                }



                var subjectDocument = workspace.GetDocument(subjectFilePath);
                if (subjectDocument == null)
                {
                    continue;
                }
                
                //var subjectProject = subjectDocument!.Project;
                //var targetNamespace = subjectProject.GetTargetNamespace(subjectFilePath);

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

                var namespaceInfos = subjectSyntaxRoot.GetAllNamespaceInfos(targetNamespace);
                if (namespaceInfos.Count == 0)
                {
                    continue;
                }

                var namespaceRenameDict = namespaceInfos.BuildRenameDict();

                //var editorProvider = new EditorProvider(workspace);

                #region fix refs (adding a new using namespace clauses)

                var toProcess = new Dictionary<string, HashSet<IFixer>>();
                var processedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                
                foreach (var foundType in subjectSyntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var symbolInfo = subjectSemanticModel.GetDeclaredSymbol(foundType);
                    if (symbolInfo == null)
                    {
                        continue;
                    }

                    if (processedTypes.Contains(symbolInfo))
                    {
                        //already processed
                        continue;
                    }

                    var targetNamespaceInfo = namespaceRenameDict[symbolInfo.ContainingNamespace.ToDisplayString()];

                    var foundReferences = await SymbolFinder.FindReferencesAsync(symbolInfo, workspace.CurrentSolution);
                    foreach (var foundReference in foundReferences)
                    {
                        if (foundReference.Definition.ContainingNamespace.ToDisplayString() == targetNamespaceInfo.ModifiedName)
                        {
                            //referenced symbols is in target namespace already
                            continue;
                        }

                        foreach (var location in foundReference.Locations)
                        {
                            if (location.Document.FilePath == null)
                            {
                                continue;
                            }
                            if (location.Location.Kind != LocationKind.SourceFile)
                            {
                                continue;
                            }

                            if (location.Location.SourceTree == null)
                            {
                                continue;
                            }

                            var refRoot = await location.Location.SourceTree.GetRootAsync();
                            if (refRoot == null)
                            {
                                continue;
                            }

                            var refSyntax = refRoot.FindNode(location.Location.SourceSpan);
                            if (refSyntax == null)
                            {
                                continue;
                            }

                            if (!toProcess.ContainsKey(location.Document.FilePath))
                            {
                                toProcess[location.Document.FilePath] = new HashSet<IFixer>(FixerEqualityComparer.Entity);
                            }

                            if (refSyntax.Parent is QualifiedNameSyntax qns)
                            {
                                //replace QualifiedNameSyntax
                                toProcess[location.Document.FilePath].Add(
                                    new QualifiedNameFixer(
                                        workspace,
                                        qns,
                                        targetNamespaceInfo.ModifiedName
                                        )
                                    );
                            }
                            else
                            {
                                //add a new using clause
                                toProcess[location.Document.FilePath].Add(
                                    new NamespaceFixer(
                                        workspace,
                                        targetNamespaceInfo.ModifiedName
                                        )
                                    );
                            }
                        }
                    }

                    processedTypes.Add(symbolInfo);
                }

                foreach (var group in toProcess)
                {
                    var targetFilePath = group.Key;

                    Debug.WriteLine($"Fix references in {targetFilePath}");

                    foreach (var fixer in group.Value.OrderBy(a => a.OrderingKey))
                    {
                        await fixer.FixAsync(targetFilePath);
                    }
                }

                #endregion

                #region fixed subject namespaces

                {
                    bool r;
                    do
                    {
                        var subjectDocumentEditor = await workspace.CreateDocumentEditorAsync(subjectDocument.FilePath!);
                        if (subjectDocumentEditor == null)
                        {
                            //skip this document
                            return;
                        }

                        foreach (var namespaceInfo in namespaceInfos.Where(ni => ni.IsRoot))
                        {
                            var syntaxRoot = await subjectDocumentEditor.OriginalDocument
                                .GetSyntaxRootAsync()
                                ;
                            if (syntaxRoot == null)
                            {
                                continue;
                            }

                            var fNamespace = syntaxRoot
                                .DescendantNodes()
                                .OfType<NamespaceDeclarationSyntax>()
                                .FirstOrDefault(mn => mn.Name.ToString() == namespaceInfo.OriginalName)
                                ;

                            var fixedNamespace = fNamespace.WithName(
                                SyntaxFactory.ParseName(
                                    namespaceInfo.ModifiedName
                                    ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                )
                                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                ;

                            subjectDocumentEditor.ReplaceNode(
                                fNamespace,
                                fixedNamespace
                                );
                        }

                        var changedDocument = subjectDocumentEditor.GetChangedDocument();
                        r = workspace.TryApplyChanges(changedDocument.Project.Solution);
                    }
                    while (!r);
                }

                #endregion

                #region fix references in xaml files

                foreach (var filePath in filePaths)
                {
                    if (!filePath.EndsWith(".xaml"))
                    {
                        continue;
                    }

                    var xamlEngine = new XamlEngine(filePath);

                    foreach (var processedType in processedTypes)
                    {
                        var targetNamespaceInfo = namespaceRenameDict[processedType.ContainingNamespace.ToDisplayString()];

                        xamlEngine.MoveObject(
                            processedType.ContainingNamespace.ToDisplayString(),
                            processedType.Name,
                            targetNamespaceInfo.ModifiedName
                            );
                    }

                    xamlEngine.SaveIfChangesExists();
                }
                #endregion

                #region remove empty 'using namespace'

                var allSolutionNamespaces = await GetAllNamespacesAsync(workspace);

                foreach (Document document in workspace.EnumerateAllDocuments(Predicate.IsProjectInScope, Predicate.IsDocumentInScope))
                {
                    if (document.FilePath == null)
                    {
                        continue;
                    }

                    var syntaxRoot = await document.GetSyntaxRootAsync();
                    if (syntaxRoot == null)
                    {
                        continue;
                    }

                    var namespaces = syntaxRoot
                        .DescendantNodes()
                        .OfType<UsingDirectiveSyntax>()
                        .ToList()
                        ;

                    if (namespaces.Count == 0)
                    {
                        continue;
                    }

                    bool r;
                    do
                    {
                        var documentEditor = await workspace.CreateDocumentEditorAsync(document.FilePath);
                        if (documentEditor == null)
                        {
                            //skip this document
                            return;
                        }


                        var changed = false;
                        foreach (var n in namespaces)
                        {
                            var nname = n.Name.ToString();

                            if (!namespaceRenameDict.ContainsKey(nname))
                            {
                                //this namespace is not what we changed
                                continue;
                            }

                            if (allSolutionNamespaces.Contains(nname))
                            {
                                //there is a types in this namespace
                                continue;
                            }

                            documentEditor.RemoveNode(n, SyntaxRemoveOptions.KeepNoTrivia);

                            changed = true;
                        }

                        if (changed)
                        {
                            var changedDocument = documentEditor.GetChangedDocument();
                            r = workspace.TryApplyChanges(changedDocument.Project.Solution);
                        }
                        else
                        {
                            r = true;
                        }

                    }
                    while (!r);

                }

                #endregion
            }


            ProgressMessage = $"Completed";
        }

        private static async Task<HashSet<string>> GetAllNamespacesAsync(VisualStudioWorkspace workspace)
        {
            var allSolutionNamespaces = 
                (await workspace.GetAllTypesInNamespaceRecursivelyAsync(null))
                    .Values
                    .Select(t => t.ContainingNamespace.ToDisplayString())
                    .ToHashSet();

            return allSolutionNamespaces;
        }
    }
}

