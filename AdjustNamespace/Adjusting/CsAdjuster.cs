using AdjustNamespace.Helper;
using AdjustNamespace.Xaml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.LanguageServices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting
{
    public class CsAdjuster
    {
        private readonly VisualStudioWorkspace _workspace;
        private readonly NamespaceCenter _namespaceCenter;
        private readonly string _subjectFilePath;
        private readonly string _targetNamespace;
        private readonly List<string> _xamlFilePaths;

        public CsAdjuster(
            VisualStudioWorkspace workspace,
            NamespaceCenter namespaceCenter,
            string subjectFilePath,
            string targetNamespace,
            List<string> xamlFilePaths
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            if (targetNamespace is null)
            {
                throw new ArgumentNullException(nameof(targetNamespace));
            }

            if (xamlFilePaths is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePaths));
            }

            _workspace = workspace;
            _namespaceCenter = namespaceCenter;
            _subjectFilePath = subjectFilePath;
            _targetNamespace = targetNamespace;
            _xamlFilePaths = xamlFilePaths;
        }

        public async Task<bool> AdjustAsync()
        {
            var subjectDocument = _workspace.GetDocument(_subjectFilePath);
            if (subjectDocument == null)
            {
                return false;
            }

            var subjectSemanticModel = await subjectDocument.GetSemanticModelAsync();
            if (subjectSemanticModel == null)
            {
                return false;
            }

            var subjectSyntaxRoot = await subjectDocument.GetSyntaxRootAsync();
            if (subjectSyntaxRoot == null)
            {
                return false;
            }

            var namespaceInfos = subjectSyntaxRoot.GetAllNamespaceInfos(_targetNamespace);
            if (namespaceInfos.Count == 0)
            {
                return false;
            }

            var namespaceRenameDict = namespaceInfos.BuildRenameDict();

            #region fix refs (adding a new using namespace clauses)

            var toProcess = new Dictionary<string, HashSet<IFixer>>();
            var processedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var foundTypeSyntax in subjectSyntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var symbolInfo = subjectSemanticModel.GetDeclaredSymbol(foundTypeSyntax);
                if (symbolInfo == null)
                {
                    //skip this type
                    continue;
                }

                if (processedTypes.Contains(symbolInfo))
                {
                    //already processed
                    continue;
                }

                var targetNamespaceInfo = namespaceRenameDict[symbolInfo.ContainingNamespace.ToDisplayString()];

                var foundReferences = await SymbolFinder.FindReferencesAsync(symbolInfo, _workspace.CurrentSolution);
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
                            //skip this location
                            continue;
                        }
                        if (location.Location.Kind != LocationKind.SourceFile)
                        {
                            //skip this location
                            continue;
                        }

                        if (location.Location.SourceTree == null)
                        {
                            //skip this location
                            continue;
                        }

                        var refRoot = await location.Location.SourceTree.GetRootAsync();
                        if (refRoot == null)
                        {
                            //skip this location
                            continue;
                        }

                        var refSyntax = refRoot.FindNode(location.Location.SourceSpan);
                        if (refSyntax == null)
                        {
                            //skip this location
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
                                    _workspace,
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
                                    _workspace,
                                    targetNamespaceInfo.ModifiedName
                                    )
                                );
                        }
                    }
                }

                processedTypes.Add(symbolInfo);
                _namespaceCenter.TypeRemoved(symbolInfo);
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

            await FixSubjectFileNamespacesAsync(
                subjectDocument,
                namespaceInfos
                );

            FixReferenceInXamlFiles(
                namespaceRenameDict,
                processedTypes
                );

            await RemoveEmptyUsingStatementsAsync();


            return true;
        }

        private async Task RemoveEmptyUsingStatementsAsync(
            )
        {

            foreach (Document document in _workspace.EnumerateAllDocuments(Predicate.IsProjectInScope, Predicate.IsDocumentInScope))
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

                bool r = true;
                do
                {
                    var documentEditor = await _workspace.CreateDocumentEditorAsync(document.FilePath);
                    if (documentEditor == null)
                    {
                        //skip this document
                        continue;
                    }


                    var changed = false;
                    foreach (var n in namespaces)
                    {
                        var nname = n.Name.ToString();

                        if (!_namespaceCenter.NamespacesToRemove.Contains(nname))
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
                        r = _workspace.TryApplyChanges(changedDocument.Project.Solution);
                    }
                }
                while (!r);
            }
        }

        private void FixReferenceInXamlFiles(
            Dictionary<string, NamespaceInfo> namespaceRenameDict, 
            HashSet<INamedTypeSymbol> processedTypes
            )
        {
            if (namespaceRenameDict is null)
            {
                throw new ArgumentNullException(nameof(namespaceRenameDict));
            }

            if (processedTypes is null)
            {
                throw new ArgumentNullException(nameof(processedTypes));
            }

            foreach (var xamlFilePath in _xamlFilePaths)
            {
                if (!xamlFilePath.EndsWith(".xaml"))
                {
                    continue;
                }

                var xamlEngine = new XamlEngine(xamlFilePath);

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
        }

        private async Task FixSubjectFileNamespacesAsync(
            Document subjectDocument, 
            List<NamespaceInfo> namespaceInfos
            )
        {
            if (subjectDocument is null)
            {
                throw new ArgumentNullException(nameof(subjectDocument));
            }

            if (namespaceInfos is null)
            {
                throw new ArgumentNullException(nameof(namespaceInfos));
            }

            bool r = true;
            do
            {
                var subjectDocumentEditor = await _workspace.CreateDocumentEditorAsync(subjectDocument.FilePath!);
                if (subjectDocumentEditor == null)
                {
                    //skip this document
                    continue;
                }

                var syntaxRoot = await subjectDocumentEditor.OriginalDocument.GetSyntaxRootAsync();
                if (syntaxRoot == null)
                {
                    //skip this document
                    continue;
                }

                var foundNamespaces = syntaxRoot
                    .DescendantNodes()
                    .OfType<NamespaceDeclarationSyntax>()
                    .ToDictionary(n => n.Name.ToString(), n => n);


                foreach (var namespaceInfo in namespaceInfos.Where(ni => ni.IsRoot))
                {
                    if (!foundNamespaces.TryGetValue(namespaceInfo.OriginalName, out var fNamespace))
                    {
                        //skip this namespace
                        continue;
                    }

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
                r = _workspace.TryApplyChanges(changedDocument.Project.Solution);
            }
            while (!r);
        }
    }
}
