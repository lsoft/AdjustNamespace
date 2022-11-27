using AdjustNamespace.Adjusting.Fixer;
using AdjustNamespace.Helper;
using AdjustNamespace.Namespace;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.VisualStudio.Language.CodeCleanUp;
using Microsoft.VisualStudio.LanguageServices;

namespace AdjustNamespace.Adjusting.Adjuster.Cs
{
    public readonly struct RefProcessor
    {
        private readonly VsServices _vss;
        private readonly FixerContainer _fixerContainer;
        private readonly NamespaceTransition _targetNamespaceInfo;

        public RefProcessor(
            VsServices vss,
            FixerContainer fixerContainer,
            NamespaceTransition targetNamespaceInfo
            )
        {
            if (fixerContainer is null)
            {
                throw new ArgumentNullException(nameof(fixerContainer));
            }

            _vss = vss;
            _fixerContainer = fixerContainer;
            _targetNamespaceInfo = targetNamespaceInfo;
        }

        public async Task ProcessRefsAsync(
            INamedTypeSymbol symbolInfo
            )
        {
            if (symbolInfo is null)
            {
                throw new ArgumentNullException(nameof(symbolInfo));
            }

            var foundReferences = await FindReferencesForAsync(_vss.Workspace, symbolInfo);

            foreach (var foundReference in foundReferences)
            {
                if (foundReference.Definition.ContainingNamespace.ToDisplayString() == _targetNamespaceInfo.ModifiedName)
                {
                    //referenced symbols is in target namespace already
                    continue;
                }

                foreach (var location in foundReference.Locations)
                {
                    await ProcessLocationAsync(location);
                }
            }
        }

        private async Task ProcessLocationAsync(
            ReferenceLocation location
            )
        {
            if (location.Document.FilePath == null)
            {
                //skip this location
                return;
            }
            if (location.Location.Kind != LocationKind.SourceFile)
            {
                //skip this location
                return;
            }

            if (location.Location.SourceTree == null)
            {
                //skip this location
                return;
            }

            var document = _vss.Workspace.GetDocument(location.Location.SourceTree.FilePath);
            if (document == null)
            {
                return;
            }

            var root = await document.GetSyntaxRootAsync();
            if (root == null)
            {
                //skip this location
                return;
            }

            var syntax = root.FindNode(location.Location.SourceSpan);
            if (syntax == null)
            {
                //skip this location
                return;
            }

            if (syntax is TypeConstraintSyntax tcs)
            {
                syntax = tcs.Type;
            }
            if (syntax is SimpleBaseTypeSyntax sbts)
            {
                syntax = sbts.Type;
            }
            if (syntax is ArgumentSyntax args)
            {
                syntax = args.Expression;
            }

            var semanticModel = await document.GetSemanticModelAsync();
            if (semanticModel == null)
            {
                return;
            }

            var symbol = semanticModel.GetSymbolInfo(syntax).Symbol;
            if (symbol == null)
            {
                return;
            }

            if (syntax.Parent is QualifiedNameSyntax qns)
            {
                ProcessQualifiedName(location, semanticModel, qns);
            }
            else if (syntax.Parent is MemberAccessExpressionSyntax maes)
            {
                var maesr = maes.ToUpperSyntax<MemberAccessExpressionSyntax>()!;

                ProcessMemberAccessExpression(
                    location,
                    syntax,
                    symbol,
                    maesr
                    );
            }
            else
            {
                //i don't know why we are here

                //add a new using clause
                _fixerContainer
                    .Fixer<AddUsingFixer>(location.Document.FilePath)
                    .AddSubject(_targetNamespaceInfo.ModifiedName);
            }
        }

        private void ProcessQualifiedName(
            ReferenceLocation location,
            SemanticModel semanticModel,
            QualifiedNameSyntax qns
            )
        {
            var uqns = qns.ToUpperSymbol(semanticModel);
            if (uqns == null)
            {
                //we found FullyQualifiedName like `Class1.NestedClass2`
                //we need to add using for this reference
                //(because these is no guarantee that namespace in THIS file
                //will be fixed, THIS file can be excluded from adjusting by the user)

                _fixerContainer
                    .Fixer<AddUsingFixer>(location.Document.FilePath!)
                    .AddSubject(_targetNamespaceInfo.ModifiedName);

                return;
            }

            //replace QualifiedNameSyntax
            var mqns = uqns
                .WithLeft(SyntaxFactory.ParseName((uqns.IsGlobal() ? "global::" : "") + " " + _targetNamespaceInfo.ModifiedName))
                .WithLeadingTrivia(uqns.GetLeadingTrivia())
                .WithTrailingTrivia(uqns.GetTrailingTrivia())
                ;

            _fixerContainer
                .Fixer<QualifiedNameFixer>(location.Document.FilePath!)
                .AddSubject(
                    new QualifiedNameFixer.QualifiedNameFixerArgument(
                        uqns.Span,
                        mqns
                        )
                    );
        }

        private void ProcessMemberAccessExpression(
            ReferenceLocation location,
            SyntaxNode syntax,
            ISymbol symbol,
            MemberAccessExpressionSyntax maes
            )
        {
            if (!symbol.Kind.NotIn(SymbolKind.Property, SymbolKind.Field, SymbolKind.Method))
            {
                _fixerContainer
                    .Fixer<AddUsingFixer>(location.Document.FilePath!)
                    .AddSubject(_targetNamespaceInfo.ModifiedName);

                return;
            }

            var isGlobal = maes.IsGlobal();

            var inss = (
                from desc in maes.DescendantNodes()
                where desc is IdentifierNameSyntax || desc is GenericNameSyntax
                select desc
                ).ToList();

            var withoutNamespaceNodes = inss
                .SkipWhile(s => !ReferenceEquals(s, syntax))
                .ToList();

            if (inss.IndexOf(syntax) <= 0) //namespace clauses exists
            {
                _fixerContainer
                    .Fixer<AddUsingFixer>(location.Document.FilePath!)
                    .AddSubject(_targetNamespaceInfo.ModifiedName);

                return;
            }

            var withoutNamespacesText = string.Join(".", withoutNamespaceNodes);

            var modifiedMaesr = SyntaxFactory.ParseExpression(
                (isGlobal ? "global::" : "") + _targetNamespaceInfo.ModifiedName + "." + withoutNamespacesText
                );

            _fixerContainer
                .Fixer<QualifiedNameFixer>(location.Document.FilePath!)
                .AddSubject(
                    new QualifiedNameFixer.QualifiedNameFixerArgument(
                        maes.Span,
                        modifiedMaesr
                        )
                    );
        }

        private static async Task<List<ReferencedSymbol>> FindReferencesForAsync(
            VisualStudioWorkspace workspace,
            INamedTypeSymbol symbolInfo
            )
        {
            var refs = await SymbolFinder.FindReferencesAsync(symbolInfo, workspace.CurrentSolution);
            var foundReferences = refs.ToList();

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
                    var methodFoundReferences = await SymbolFinder.FindReferencesAsync(extensionMethodSymbol, workspace.CurrentSolution);
                    foundReferences.AddRange(
                        methodFoundReferences
                        );
                }
            }

            return foundReferences;
        }

    }
}
