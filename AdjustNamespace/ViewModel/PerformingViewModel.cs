using AdjustNamespace.Helper;
using AdjustNamespace.Mover;
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

            for (var i = 0; i < _subjectFilePaths.Count; i++)
            {
                var subjectFilePath = _subjectFilePaths[i];

                ProgressMessage = $"File #{i + 1} out of #{_subjectFilePaths.Count}";

                var subjectDocument = workspace.GetDocument(subjectFilePath);
                var subjectProject = subjectDocument!.Project;
                var targetNamespace = subjectProject.GetTargetNamespace(subjectFilePath);

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

                var editorProvider = new EditorProvider(workspace);

                #region fix refs (adding a new using namespace clauses)

                var toProcess = new Dictionary<string, HashSet<IFixer>>();
                var processedTypes = new HashSet<string>();

                foreach (var foundType in subjectSyntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
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

                    var targetNamespaceInfo = namespaceRenameDict[symbolInfo.ContainingNamespace.ToDisplayString()];

                    var foundReferences = await SymbolFinder.FindReferencesAsync(symbolInfo, workspace.CurrentSolution);
                    foreach (var foundReference in foundReferences)
                    {
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

                            var refSyntax = location.Location.SourceTree?.GetRoot().FindNode(location.Location.SourceSpan);
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
                                        targetNamespaceInfo.ModifiedName
                                        )
                                    );
                            }
                        }
                    }

                    processedTypes.Add(symbolInfo.ToDisplayString());
                }

                foreach (var group in toProcess)
                {
                    var targetFilePath = group.Key;

                    var documentEditor = await editorProvider.GetDocumentEditorAsync(targetFilePath);
                    if (documentEditor == null)
                    {
                        //skip this document
                        continue;
                    }

                    foreach (var fixer in group.Value.OrderByDescending(a => a.OrderingKey))
                    {
                        await fixer.FixAsync(documentEditor);
                    }
                }

                #endregion

                #region fixed subject namespaces

                {
                    var subjectDocumentEditor = await editorProvider.GetDocumentEditorAsync(subjectDocument.FilePath!);

                    if (subjectDocumentEditor != null)
                    {
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
                    }

                    await editorProvider.SaveAndClearAsync();

                    //remove `using oldnamespace` if oldnamespace does not exists anymore
                    foreach (var document in workspace.EnumerateAllDocuments(Predicate.IsProjectInScope, Predicate.IsDocumentInScope))
                    {
                        Debug.WriteLine(document.FilePath);

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
                            if (namespaceRenameDict.Keys.Contains(usingSyntax.Name.ToString()))
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

                await editorProvider.SaveAndClearAsync();

                #endregion

                #region fix xaml

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var xamlsToProcess = new List<string>();

                var filePaths = dte.Solution.ProcessSolution(workspace);

                //foreach (EnvDTE.Project project in dte.Solution.Projects)
                //{
                //    foreach (EnvDTE.ProjectItem projectItem in project.ProjectItems)
                //    {
                //        if (projectItem == null)
                //        {
                //            continue;
                //        }

                //        for (var fi = 0; fi < projectItem.FileCount; fi++)
                //        {
                //            var projectItemFilePath = projectItem.FileNames[(short)fi]; 123123

                //            if (projectItemFilePath.EndsWith(".xaml"))
                //            {
                //                xamlsToProcess.Add(projectItemFilePath);
                //            }
                //        }
                //    }
                //}

                await TaskScheduler.Default;

                foreach (var filePath in filePaths)
                {
                    if (!filePath.EndsWith(".xaml"))
                    {
                        continue;
                    }

                    var xamlToProcess = filePath;

                    var plainBody = File.ReadAllText(xamlToProcess);
                    var xmlDocument = XDocument.Load(xamlToProcess, LoadOptions.PreserveWhitespace);

                    var changedExists = false;

                    var element = xmlDocument.Elements().FirstOrDefault();
                    if (element == null)
                    {
                        continue;
                    }

                    var attributes = new List<XAttribute>(element.Attributes());
                    foreach (var attribute in attributes)
                    {
                        if (attribute.Name.Namespace == "http://www.w3.org/2000/xmlns/")
                        {
                            const string ClrNamespace = "clr-namespace:";

                            if (attribute.Value.Length <= ClrNamespace.Length)
                            {
                                continue;
                            }

                            var modifiedValue = attribute.Value.Substring(ClrNamespace.Length);
                            if (namespaceRenameDict.TryGetValue(modifiedValue, out var namespaceInfo))
                            {
                                plainBody = plainBody.Replace(
                                    attribute.Value,
                                    $"{ClrNamespace}{namespaceInfo.ModifiedName}"
                                    );

                                changedExists = true;
                            }
                        }
                    }

                    if (changedExists)
                    {
                        File.WriteAllText(xamlToProcess, plainBody);
                    }
                }

            }

            #endregion

            ProgressMessage = $"Completed";
        }
    }
}

