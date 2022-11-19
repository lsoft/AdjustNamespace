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
using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Namespace;

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// Adjuster for cs file.
    /// </summary>
    public class CsAdjuster : IAdjuster
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

            var ntc = NamespaceTransitionContainer.GetNamespaceTransitionsFor(subjectSyntaxRoot, _targetNamespace);
            if (ntc.IsEmpty)
            {
                //skip this document
                return false;
            }

            #region fix refs (adding a new using namespace clauses or edit fully qualified names)

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
                var targetNamespaceInfo = ntc.TransitionDict[symbolNamespace];

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
                            var uqns = qns.Upper(refSemanticModel);
                            if(uqns != null)
                            {
                                //replace QualifiedNameSyntax
                                var mqns = uqns
                                    .WithLeft(SyntaxFactory.ParseName((uqns.IsGlobal() ? "global::" : "") + " " + targetNamespaceInfo.ModifiedName))
                                    .WithLeadingTrivia(uqns.GetLeadingTrivia())
                                    .WithTrailingTrivia(uqns.GetTrailingTrivia())
                                    ;

                                fixerContainer
                                    .Fixer<QualifiedNameFixer>(location.Document.FilePath)
                                    .AddSubject(
                                        new QualifiedNameFixer.QualifiedNameFixerArgument(
                                            uqns,
                                            mqns
                                            )
                                        );
                            }
                            else
                            {
                                //we found FullyQualifiedName like `Class1.NestedClass2`
                                //we need to add using for this reference
                                //(because these is no guarantee that namespace in THIS file
                                //will be fixed, THIS file can be excluded from adjusting by the user)

                                fixerContainer
                                    .Fixer<AddUsingFixer>(location.Document.FilePath)
                                    .AddSubject(
                                        targetNamespaceInfo.ModifiedName
                                        );
                            }
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
                                        .Fixer<AddUsingFixer>(location.Document.FilePath)
                                        .AddSubject(targetNamespaceInfo.ModifiedName);
                                }
                            }
                            else
                            {
                                fixerContainer
                                    .Fixer<AddUsingFixer>(location.Document.FilePath)
                                    .AddSubject(targetNamespaceInfo.ModifiedName);
                            }
                        }
                        else
                        {
                            //i don't know why we are here

                            //add a new using clause
                            fixerContainer
                                .Fixer<AddUsingFixer>(location.Document.FilePath)
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
                ntc
                );

            FixReferenceInXamlFiles(
                ntc,
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
                        //something went wrong
                        //skip this document
                        return;
                    }

                    var namespaces = syntaxRoot.GetAllDescendants<UsingDirectiveSyntax>();

                    var toRemove = _namespaceCenter.GetRemovedNamespaces(namespaces);
                    if (toRemove.Count == 0)
                    {
                        continue;
                    }

                    syntaxRoot = syntaxRoot.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepNoTrivia);
                    if (syntaxRoot != null)
                    {
                        var changedDocument = document.WithSyntaxRoot(syntaxRoot);

                        r = _workspace.TryApplyChanges(changedDocument.Project.Solution);
                    }
                }
                while (!r);
            }
        }

        private void FixReferenceInXamlFiles(
            NamespaceTransitionContainer ntc,
            HashSet<INamedTypeSymbol> processedTypes
            )
        {
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
                    var targetNamespaceInfo = ntc.TransitionDict[processedType.ContainingNamespace.ToDisplayString()];

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
            NamespaceTransitionContainer ntc
            )
        {
            if (subjectDocument is null)
            {
                throw new ArgumentNullException(nameof(subjectDocument));
            }

            foreach (var transition in ntc.Transitions.Where(ni => ni.IsRoot))
            {
                bool r = true;
                do
                {
                    var (openedDocument, syntaxRoot) = await _workspace.GetDocumentAndSyntaxRootAsync(subjectDocument.FilePath!);
                    if (openedDocument == null || syntaxRoot == null)
                    {
                        //something went wrong
                        //skip this document
                        return;
                    }

                    if (!syntaxRoot.TryFindNamespaceNodesFor(transition.OriginalName, out var ufNamespaces))
                    {
                        //skip this namespace
                        break;
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
                    if (cus == null)
                    {
                        //skip this namespace
                        break;
                    }
                    var newUsingStatement = SyntaxFactory.UsingDirective(
                        SyntaxFactory.ParseName(
                            " " + ufNamespace!.Name
                            )
                        ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    cus = cus.AddUsings(newUsingStatement);

                    if(!cus.TryFindNamespaceNodesFor(transition.OriginalName, out var fNamespaces))
                    {
                        //skip this namespace
                        break;
                    }

                    foreach (var fNamespace in fNamespaces!)
                    {
                        var newName = SyntaxFactory.ParseName(
                            transition.ModifiedName
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
