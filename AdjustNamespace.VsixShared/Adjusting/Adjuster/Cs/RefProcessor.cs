using AdjustNamespace.Adjusting.Fixer;
using AdjustNamespace.Helper;
using AdjustNamespace.Namespace;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
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
    /// <summary>
    /// Processor of the references to a type which is being moved into another namespace.
    /// For every found reference it creates a fixer:
    /// <list type="bullet">
    /// <item>a fully qualified name (<c>Some.Old.Namespace.Class1</c>) is rewritten in place
    /// with <see cref="QualifiedNameFixer"/>;</item>
    /// <item>in all the other cases a new `using` clause is added with <see cref="AddUsingFixer"/>.</item>
    /// </list>
    /// The fixers are not applied here, they are accumulated in the <see cref="FixerContainer"/>
    /// and applied later all at once.
    /// </summary>
    public readonly struct RefProcessor
    {
        private readonly VsServices _vss;
        private readonly FixerContainer _fixerContainer;
        private readonly NamespaceTransition _targetNamespaceInfo;

        /// <param name="vss">Visual Studio services.</param>
        /// <param name="fixerContainer">Container the created fixers are placed into.</param>
        /// <param name="targetNamespaceInfo">Transition (old namespace -> new namespace) of the processed type.</param>
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

        /// <summary>
        /// Find all the references to the given type across the solution and create a fixer for each of them.
        /// </summary>
        /// <param name="symbolInfo">The type which is being moved into another namespace.</param>
        public async Task ProcessRefsAsync(
            INamedTypeSymbol symbolInfo
            )
        {
            if (symbolInfo is null)
            {
                throw new ArgumentNullException(nameof(symbolInfo));
            }

            var foundReferences = await FindReferencesForAsync(_vss.Workspace, symbolInfo);

            //a file which several projects compile produces a separate symbol per project,
            //and Roslyn cascades the search to all of them: the very same location is reported
            //once per project which compiles the file it lives in
            var processedLocations = new HashSet<(string, TextSpan)>();

            foreach (var foundReference in foundReferences)
            {
                if (foundReference.Definition.ContainingNamespace.ToDisplayString() == _targetNamespaceInfo.ModifiedName)
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

                    if (!processedLocations.Add((location.Document.FilePath, location.Location.SourceSpan)))
                    {
                        //this location has been reported by another project already
                        continue;
                    }

                    await ProcessLocationAsync(location);
                }
            }
        }

        /// <summary>
        /// Determine the kind of the syntax at the reference location and create a suitable fixer for it.
        /// A location we are unable to understand is skipped silently.
        /// </summary>
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

            //the document of the location and not the document of that file in the current
            //context: the span of the location belongs to the tree of that very document,
            //and the trees of one file may differ (a reference under `#if NET8_0` is a code
            //for one project which compiles the file and a disabled text for another one)
            var document = location.Document;

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

            //FindNode returns the closest enclosing node, which is not always the reference itself
            //(`where T : Class1`, `class A : Class1`, `Foo(Class1.Bar)`),
            //so we need to descend to the real type reference syntax
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

        /// <summary>
        /// Process a reference which is a part of a qualified name (<c>Some.Old.Namespace.Class1</c>).
        /// The namespace part of that name is replaced with the target namespace.
        /// </summary>
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
                .WithLeft(SyntaxFactory.ParseName((uqns.IsGlobal() ? "global::" : "") + _targetNamespaceInfo.ModifiedName))
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

        /// <summary>
        /// Process a reference which is a part of a member access expression
        /// (<c>Some.Old.Namespace.Class1.StaticMember</c>).
        /// If the namespace is written explicitly, the whole expression is rebuilt
        /// with the target namespace; otherwise a new `using` clause is enough.
        /// </summary>
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

            var inss = GetChainOf(maes);

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

        /// <summary>
        /// The parts of a member access expression (<c>A.B.Class1.Value</c>) from the left
        /// to the right: the innermost expression plus the name of every access.
        ///
        /// Only these nodes belong to the chain. A plain scan for the identifiers of the
        /// expression would also give the type arguments of a generic type
        /// (<c>Foo</c> of <c>A.B.Class1&lt;Foo&gt;.Value</c>) and would insert them
        /// into the middle of the rebuilt expression.
        /// </summary>
        private static List<SyntaxNode> GetChainOf(MemberAccessExpressionSyntax maes)
        {
            var result = new List<SyntaxNode>();

            ExpressionSyntax current = maes;
            while (current is MemberAccessExpressionSyntax m)
            {
                result.Insert(0, m.Name);
                current = m.Expression;
            }

            result.Insert(0, current);

            return result;
        }

        /// <summary>
        /// Find all the references to the given type.
        /// Roslyn does not report the usages of the extension methods as the references
        /// to their containing static class, so such methods are queried additionally.
        /// </summary>
        private static async Task<List<ReferencedSymbol>> FindReferencesForAsync(
            Workspace workspace,
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
