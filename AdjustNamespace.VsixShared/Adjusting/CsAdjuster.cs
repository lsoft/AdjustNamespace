﻿using AdjustNamespace.Helper;
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

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// Adjuster for cs file.
    /// </summary>
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
            var (subjectDocument, subjectSyntaxRoot) = await _workspace.GetDocumentAndSyntaxRootAsync(_subjectFilePath);
            if (subjectDocument == null || subjectSyntaxRoot == null)
            {
                //skip this document
                return false;
            }

            var subjectSemanticModel = await subjectDocument.GetSemanticModelAsync();
            if (subjectSemanticModel == null)
            {
                //skip this document
                return false;
            }

            var namespaceInfos = subjectSyntaxRoot.GetAllNamespaceInfos(_targetNamespace);
            if (namespaceInfos.Count == 0)
            {
                //skip this document
                return false;
            }

            var namespaceRenameDict = namespaceInfos.BuildRenameDict();

            #region fix refs (adding a new using namespace clauses)

            var fixerContainer = new FixerContainer(_workspace);
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

                        if (refSyntax is TypeConstraintSyntax tcs)
                        {
                            refSyntax = tcs.Type;
                        }
                        if (refSyntax is SimpleBaseTypeSyntax sbts)
                        {
                            refSyntax = sbts.Type;
                        }
                        if (refSyntax is ArgumentSyntax args)
                        {
                            refSyntax = args.Expression;
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

                        fixerContainer.TryAddFixersFor(location.Document.FilePath);

                        if (refSyntax.Parent is QualifiedNameSyntax qns)
                        {
                            //replace QualifiedNameSyntax
                            var mqns = qns
                                .WithLeft(SyntaxFactory.ParseName((qns.IsGlobal() ? "global::" : "") + " " + targetNamespaceInfo.ModifiedName))
                                .WithLeadingTrivia(qns.GetLeadingTrivia())
                                .WithTrailingTrivia(qns.GetTrailingTrivia())
                                ;

                            fixerContainer
                                .Fixer<QualifiedNameFixer>(location.Document.FilePath)
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
                                var isGlobal = maesr.IsGlobal();

                                var inss = (
                                    from desc in maesr.DescendantNodes()
                                    where desc is IdentifierNameSyntax || desc is GenericNameSyntax
                                    select desc
                                    ).ToList();

                                var withoutNamespaceNodes = inss
                                    .SkipWhile(s => !ReferenceEquals(s, refSyntax))
                                    .ToList();

                                var withoutNamespacesText = string.Join(".", withoutNamespaceNodes);

                                if(inss.IndexOf(refSyntax) > 0) //namespace clauses exists
                                {
                                    var modifiedMaesr = SyntaxFactory.ParseExpression(
                                        (isGlobal ? "global::" : "") + targetNamespaceInfo.ModifiedName + "." + withoutNamespacesText
                                        );

                                    fixerContainer
                                        .Fixer<QualifiedNameFixer>(location.Document.FilePath)
                                        .AddSubject(
                                            new QualifiedNameFixer.QualifiedNameFixerArgument(
                                                maesr,
                                                modifiedMaesr
                                                )
                                            );
                                }
                                else
                                {
                                    fixerContainer
                                        .Fixer<NamespaceFixer>(location.Document.FilePath)
                                        .AddSubject(targetNamespaceInfo.ModifiedName);
                                }
                            }
                            else
                            {
                                fixerContainer
                                    .Fixer<NamespaceFixer>(location.Document.FilePath)
                                    .AddSubject(targetNamespaceInfo.ModifiedName);
                            }
                        }
                        else
                        {
                            //i don't know why we are here

                            //add a new using clause
                            fixerContainer
                                .Fixer<NamespaceFixer>(location.Document.FilePath)
                                .AddSubject(targetNamespaceInfo.ModifiedName);
                        }
                    }
                }

                processedTypes.Add(symbolInfo);
                _namespaceCenter.TypeRemoved(symbolInfo);
            }

            foreach (var pair in fixerContainer.Dict)
            {
                var targetFilePath = pair.Key;

                Debug.WriteLine($"Fix references in {targetFilePath}");

                await pair.Value.FixAllAsync();
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
                    var (document, syntaxRoot) = await _workspace.GetDocumentAndSyntaxRootAsync(documentFilePath);
                    if (document == null || syntaxRoot == null)
                    {
                        //skip this document
                        return;
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
                    var (openedDocument, syntaxRoot) = await _workspace.GetDocumentAndSyntaxRootAsync(subjectDocument.FilePath!);
                    if (openedDocument == null || syntaxRoot == null)
                    {
                        //skip this document
                        return;
                    }

                    if (!syntaxRoot.TryFindNamespaceNodesFor(namespaceInfo, out var ufNamespaces))
                    {
                        //skip this namespace
                        continue;
                    }

                    var ufNamespace = ufNamespaces.First();

                    //class A : IA {}
                    //we're moving A into a different namespace, but IA are not.
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
                                " " + ufNamespace!.Name
                                )
                            ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                        cus = cus.AddUsings(newUsingStatement);
                    }

                    if(!cus!.TryFindNamespaceNodesFor(namespaceInfo, out var fNamespaces))
                    {
                        //skip this namespace
                        continue;
                    }

                    foreach (var fNamespace in fNamespaces!)
                    {
                        var newName = SyntaxFactory.ParseName(
                            namespaceInfo.ModifiedName
                            );

                        if (fNamespace is NamespaceDeclarationSyntax)
                        {
                            newName = newName
                                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                ;
                        }

                        var fixedNamespace = fNamespace!.WithName(
                            newName
                            )
                            .WithLeadingTrivia(fNamespace.GetLeadingTrivia())
                            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                            ;

                        cus = cus!.ReplaceNode(
                            fNamespace,
                            fixedNamespace
                            );
                    }

                    openedDocument = openedDocument.WithSyntaxRoot(cus!);

                    r = _workspace.TryApplyChanges(openedDocument.Project.Solution);
                }
                while (!r);
            }
        }
    }
}