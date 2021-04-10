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
using System.Threading.Tasks;
using AdjustNamespace.Adjusting.Fixer;
using System.Threading;

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

            var toProcess = new Dictionary<string, List<IFixer>>();
            var processedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            var foundSyntaxes = (
                from snode in subjectSyntaxRoot.DescendantNodes()
                where snode is TypeDeclarationSyntax || snode is EnumDeclarationSyntax || snode is DelegateDeclarationSyntax
                select snode
                ).ToList();

            foreach (var foundTypeSyntax in foundSyntaxes)
            {
                var symbolInfo = (INamedTypeSymbol?)subjectSemanticModel.GetDeclaredSymbol(foundTypeSyntax);
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

                var symbolNamespace = symbolInfo.ContainingNamespace.ToDisplayString();
                var targetNamespaceInfo = namespaceRenameDict[symbolNamespace];

                if (symbolNamespace == targetNamespaceInfo.ModifiedName)
                {
                    //current symbol is in target namespace already
                    continue;
                }

                var foundReferences = (await SymbolFinder.FindReferencesAsync(symbolInfo, _workspace.CurrentSolution))
                    .Select(r => (Reference: r, IsClass : true))
                    .ToList();

                if (symbolInfo.TypeKind == TypeKind.Class && symbolInfo.IsStatic)
                {
                    var extensionMethodSymbols = (
                        from member in symbolInfo.GetMembers()
                        where member is IMethodSymbol
                        let method = member as IMethodSymbol
                        where method.IsStatic
                        where method.IsExtensionMethod
                        select method
                        )
                        .ToList();

                    foreach (var extensionMethodSymbol in extensionMethodSymbols)
                    {
                        var methodFoundReferences = await SymbolFinder.FindReferencesAsync(extensionMethodSymbol, _workspace.CurrentSolution);
                        foundReferences.AddRange(
                            methodFoundReferences.Select(r => (Reference: r, IsClass: false)
                            ));
                    }
                }

                foreach (var foundReferencePair in foundReferences)
                {
                    var foundReference = foundReferencePair.Reference;
                    var isClass = foundReferencePair.IsClass;

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

                        var refDocument = _workspace.GetDocument(location.Location.SourceTree.FilePath);
                        if (refDocument == null)
                        {
                            continue;
                        }

                        var refRoot = await refDocument.GetSyntaxRootAsync();
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

                        var refSemanticModel = await refDocument.GetSemanticModelAsync();
                        if (refSemanticModel == null)
                        {
                            continue;
                        }

                        var refSymbol = refSemanticModel.GetSymbolInfo(refSyntax).Symbol;
                        if (refSymbol == null)
                        {
                            continue;
                        }


                        if (!toProcess.ContainsKey(location.Document.FilePath))
                        {
                            toProcess[location.Document.FilePath] = new List<IFixer>
                            {
                                new QualifiedNameFixer(_workspace),
                                new NamespaceFixer(_workspace)
                            };
                        }

                        //if (isClass)
                        //{
                        //    if (refSyntax.Parent is QualifiedNameSyntax qns)
                        //    {
                        //        //replace QualifiedNameSyntax
                        //        var mqns = qns
                        //            .WithLeft(SyntaxFactory.ParseName((qns.IsGlobal() ? "global::" : "") + " " + targetNamespaceInfo.ModifiedName))
                        //            .WithLeadingTrivia(qns.GetLeadingTrivia())
                        //            .WithTrailingTrivia(qns.GetTrailingTrivia())
                        //            ;

                        //        toProcess[location.Document.FilePath].First(f => f.GetType() == typeof(QualifiedNameFixer))
                        //            .AddSubject(
                        //                new QualifiedNameFixer.QualifiedNameFixerArgument(
                        //                    qns,
                        //                    mqns
                        //                    )
                        //            );
                        //    }
                        //    else
                        //    {
                        //        //add a new using clause
                        //        toProcess[location.Document.FilePath].First(f => f.GetType() == typeof(NamespaceFixer))
                        //            .AddSubject(targetNamespaceInfo.ModifiedName);
                        //    }
                        //}
                        //else
                        {
                            if (refSyntax.Parent is QualifiedNameSyntax qns)
                            {
                                //replace QualifiedNameSyntax
                                var mqns = qns
                                    .WithLeft(SyntaxFactory.ParseName((qns.IsGlobal() ? "global::" : "") + " " + targetNamespaceInfo.ModifiedName))
                                    .WithLeadingTrivia(qns.GetLeadingTrivia())
                                    .WithTrailingTrivia(qns.GetTrailingTrivia())
                                    ;

                                toProcess[location.Document.FilePath].First(f => f.GetType() == typeof(QualifiedNameFixer))
                                    .AddSubject(
                                        new QualifiedNameFixer.QualifiedNameFixerArgument(
                                            qns,
                                            mqns
                                            )
                                    );
                            }
                            else if (refSyntax.Parent is MemberAccessExpressionSyntax maes)
                            {
                                var maesr = maes.UpTo<MemberAccessExpressionSyntax>()!;

                                if (refSymbol.Kind.NotIn(SymbolKind.Property, SymbolKind.Field, SymbolKind.Method))
                                {
                                    if (maesr.Expression is MemberAccessExpressionSyntax typeNameExpression)
                                    {
                                        var modifiedTypeNameExpression = SyntaxFactory.ParseExpression(
                                            (typeNameExpression.IsGlobal() ? "global::" : "") + targetNamespaceInfo.ModifiedName + "." + typeNameExpression.Name
                                            );

                                        toProcess[location.Document.FilePath].First(f => f.GetType() == typeof(QualifiedNameFixer))
                                            .AddSubject(
                                                new QualifiedNameFixer.QualifiedNameFixerArgument(
                                                    typeNameExpression,
                                                    modifiedTypeNameExpression
                                                    )
                                            );
                                    }
                                    else
                                    {
                                        toProcess[location.Document.FilePath].First(f => f.GetType() == typeof(NamespaceFixer))
                                            .AddSubject(targetNamespaceInfo.ModifiedName);
                                    }
                                }
                                else
                                {
                                    //if (targetExpression is MemberAccessExpressionSyntax typeNameExpression)
                                    //{
                                    //    var modifiedTypeNameExpression = SyntaxFactory.ParseExpression(
                                    //        (typeNameExpression.IsGlobal() ? "global::" : "") + targetNamespaceInfo.ModifiedName + "." + typeNameExpression.Name
                                    //        );

                                    //    toProcess[location.Document.FilePath].First(f => f.GetType() == typeof(QualifiedNameFixer))
                                    //        .AddSubject(
                                    //            new QualifiedNameFixer.QualifiedNameFixerArgument(
                                    //                typeNameExpression,
                                    //                modifiedTypeNameExpression
                                    //                )
                                    //        );
                                    //}
                                    //else
                                    {
                                        toProcess[location.Document.FilePath].First(f => f.GetType() == typeof(NamespaceFixer))
                                            .AddSubject(targetNamespaceInfo.ModifiedName);
                                    }
                                }
                            }
                            else
                            {
                                //i don't know why we are here

                                //add a new using clause
                                toProcess[location.Document.FilePath].First(f => f.GetType() == typeof(NamespaceFixer))
                                    .AddSubject(targetNamespaceInfo.ModifiedName);
                            }
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

                var qnf = group.Value.First(f => f.GetType() == typeof(QualifiedNameFixer));
                await qnf.FixAsync(targetFilePath);

                var nsf = group.Value.First(f => f.GetType() == typeof(NamespaceFixer));
                await nsf.FixAsync(targetFilePath);
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
            foreach (var documentFilePath in _workspace.EnumerateAllDocumentFilePaths(Predicate.IsProjectInScope, Predicate.IsDocumentInScope))
            {
                if (documentFilePath == null)
                {
                    continue;
                }

                bool r = true;
                do
                {
                    var document = _workspace.GetDocument(documentFilePath);
                    if (document == null)
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

                    var toRemove = new List<SyntaxNode>();

                    foreach (var n in namespaces)
                    {
                        var nname = n.Name.ToString();

                        if (!_namespaceCenter.NamespacesToRemove.Contains(nname))
                        {
                            //there is a types in this namespace
                            continue;
                        }

                        toRemove.Add(n);
                    }

                    if (toRemove.Count > 0)
                    {
                        syntaxRoot = syntaxRoot!.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepNoTrivia);
                        if (syntaxRoot != null)
                        {
                            var changedDocument = document.WithSyntaxRoot(syntaxRoot);

                            r = _workspace.TryApplyChanges(changedDocument.Project.Solution);
                        }
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

            foreach (var namespaceInfo in namespaceInfos.Where(ni => ni.IsRoot))
            {
                bool r = true;
                do
                {
                    var openedDocument = _workspace.GetDocument(subjectDocument.FilePath!);
                    if (openedDocument == null)
                    {
                        //skip this document
                        return;
                    }

                    var syntaxRoot = await openedDocument.GetSyntaxRootAsync();
                    if (syntaxRoot == null)
                    {
                        //skip this document
                        return;
                    }

                    if (!TryFindNamespaceNode(syntaxRoot, namespaceInfo, out var fNamespace))
                    {
                        //skip this namespace
                        continue;
                    }

                    //class a : ia {}
                    //we're moving a into a different namespace, but ia are not
                    //we need to insert 'using old namespace'
                    //otherwise ia will not be resolved

                    //we can't determite it is the case or it's not without a costly analysis
                    //it's a subject for a future work
                    //so add at 100% cases now

                    var cus = syntaxRoot as CompilationUnitSyntax;
                    if (cus != null)
                    {
                        var newUsingStatement = SyntaxFactory.UsingDirective(
                            SyntaxFactory.ParseName(
                                " " + fNamespace!.Name
                                )
                            ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                        cus = cus.AddUsings(newUsingStatement);
                    }

                    if(!TryFindNamespaceNode(cus!, namespaceInfo, out fNamespace))
                    {
                        //skip this namespace
                        continue;
                    }


                    var fixedNamespace = fNamespace!.WithName(
                        SyntaxFactory.ParseName(
                            namespaceInfo.ModifiedName
                            ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                        )
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                        ;

                    cus = cus!.ReplaceNode(
                        fNamespace,
                        fixedNamespace
                        );


                    openedDocument = openedDocument.WithSyntaxRoot(cus!);

                    r = _workspace.TryApplyChanges(openedDocument.Project.Solution);
                }
                while (!r);
            }
        }

        private bool TryFindNamespaceNode(
            SyntaxNode syntaxRoot,
            NamespaceInfo namespaceInfo,
            out NamespaceDeclarationSyntax? fNamespace
            )
        {
            if (syntaxRoot is null)
            {
                throw new ArgumentNullException(nameof(syntaxRoot));
            }

            if (namespaceInfo is null)
            {
                throw new ArgumentNullException(nameof(namespaceInfo));
            }

            var foundNamespaces = syntaxRoot
                .DescendantNodes()
                .OfType<NamespaceDeclarationSyntax>()
                .ToDictionary(n => n.Name.ToString(), n => n)
                ;


            foundNamespaces.TryGetValue(namespaceInfo.OriginalName, out fNamespace);

            return fNamespace != null;
        }
    }
}
