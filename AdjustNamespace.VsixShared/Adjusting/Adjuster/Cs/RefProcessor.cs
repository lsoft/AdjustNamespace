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
            //a case of a union (`union Pet(Cat, Dog);`) is a parameter which consists of
            //its type only, so the span of the reference is the span of the whole parameter
            //and FindNode stops at it. An ordinary parameter has a name behind its type and
            //is never the closest node of such a span.
            if (syntax is ParameterSyntax ps
                && ps.Type != null
                && ps.Identifier.IsKind(SyntaxKind.None)
                )
            {
                syntax = ps.Type;
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
                ProcessQualifiedName(location, semanticModel, syntax, symbol.Name, qns);
            }
            else if (syntax.Parent is MemberAccessExpressionSyntax maes)
            {
                var maesr = maes.ToUpperSyntax<MemberAccessExpressionSyntax>()!;

                ProcessMemberAccessExpression(
                    location,
                    semanticModel,
                    syntax,
                    symbol,
                    maesr
                    );
            }
            else
            {
                //i don't know why we are here

                if (syntax is SimpleNameSyntax sns
                    && sns.Identifier.ValueText == symbol.Name
                    && TryFixShadowedReference(location, semanticModel, sns, symbol.Name)
                    )
                {
                    //a using clause would not help here, the name has been qualified instead
                    return;
                }

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
        /// <param name="location">The reference location.</param>
        /// <param name="semanticModel">Semantic model of the document the reference lives in.</param>
        /// <param name="syntax">The node of the reference itself, i.e. the name of the moved type.</param>
        /// <param name="typeName">The name of the moved type.</param>
        /// <param name="qns">The qualified name the reference is a part of.</param>
        private void ProcessQualifiedName(
            ReferenceLocation location,
            SemanticModel semanticModel,
            SyntaxNode syntax,
            string typeName,
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

                if (ReferenceEquals(qns.Left, syntax)
                    && syntax is SimpleNameSyntax sns
                    && sns.Identifier.ValueText == typeName
                    && TryFixShadowedReference(
                        location,
                        semanticModel,
                        qns.ToUpperSyntax<QualifiedNameSyntax>()!,
                        typeName
                        )
                    )
                {
                    //a using clause would not help here, the name has been qualified instead
                    return;
                }

                _fixerContainer
                    .Fixer<AddUsingFixer>(location.Document.FilePath!)
                    .AddSubject(_targetNamespaceInfo.ModifiedName);

                return;
            }

            var isGlobal = uqns.IsGlobal()
                || IsGlobalPrefixRequired(semanticModel, uqns.SpanStart, _targetNamespaceInfo.ModifiedName);

            //replace QualifiedNameSyntax
            var mqns = uqns
                .WithLeft(SyntaxFactory.ParseName((isGlobal ? "global::" : "") + _targetNamespaceInfo.ModifiedName))
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
            SemanticModel semanticModel,
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

            var isGlobal = maes.IsGlobal()
                || IsGlobalPrefixRequired(semanticModel, maes.SpanStart, _targetNamespaceInfo.ModifiedName);

            var inss = GetChainOf(maes);

            var withoutNamespaceNodes = inss
                .SkipWhile(s => !ReferenceEquals(s, syntax))
                .ToList();

            var typeIndex = inss.IndexOf(syntax);

            //`typeIndex == 0` means that no namespace is written in front of the type name
            //(`Class1.StaticMember`): a new using clause is enough, unless the short name
            //of the type is shadowed by the target namespace and does not resolve to it
            //anymore, see IsShadowedByTargetNamespace.
            //`typeIndex < 0` means that we do not understand this chain at all
            if (typeIndex < 0
                || (typeIndex == 0 && !IsShadowedByTargetNamespace(semanticModel, maes.SpanStart, symbol.Name))
                )
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
        /// Qualify a reference which a using clause is unable to fix, see
        /// <see cref="IsShadowedByTargetNamespace"/>: the target namespace is written
        /// in front of the name instead of being imported.
        /// </summary>
        /// <param name="location">The reference location.</param>
        /// <param name="semanticModel">Semantic model of the document the reference lives in.</param>
        /// <param name="nodeToReplace">
        /// The whole name to be qualified: the name of the type itself (<c>Class1</c>)
        /// or the name it is the head of (<c>Class1.NestedClass2</c>).
        /// </param>
        /// <param name="typeName">The name of the moved type, i.e. the head of that name.</param>
        /// <returns><c>false</c> if the name resolves to the type as it is and needs no qualification.</returns>
        private bool TryFixShadowedReference(
            ReferenceLocation location,
            SemanticModel semanticModel,
            SyntaxNode nodeToReplace,
            string typeName
            )
        {
            if (!IsShadowedByTargetNamespace(semanticModel, nodeToReplace.SpanStart, typeName))
            {
                return false;
            }

            //`global::Class1` is a name of the root namespace already: the target namespace
            //is written after that alias and not in front of it
            var isAliasQualified = nodeToReplace.Parent is AliasQualifiedNameSyntax;

            var isGlobal = !isAliasQualified
                && (nodeToReplace.IsGlobal()
                    || IsGlobalPrefixRequired(semanticModel, nodeToReplace.SpanStart, _targetNamespaceInfo.ModifiedName)
                    );

            var modified = SyntaxFactory.ParseName(
                (isGlobal ? "global::" : "") + _targetNamespaceInfo.ModifiedName + "." + nodeToReplace.ToString()
                );

            _fixerContainer
                .Fixer<QualifiedNameFixer>(location.Document.FilePath!)
                .AddSubject(
                    new QualifiedNameFixer.QualifiedNameFixerArgument(
                        nodeToReplace.Span,
                        modified
                        )
                    );

            return true;
        }

        /// <summary>
        /// The target namespace ends with the name of the type which is being moved into it
        /// (<c>Class1</c> goes into <c>A.B.Class1</c>), and that namespace is a member of one
        /// of the namespaces enclosing the reference.
        ///
        /// The members of an enclosing namespace are looked up before the using clauses of
        /// the file, so the short name of such a type resolves to the namespace and not to
        /// the type anymore: no using clause is able to fix such a reference (CS0118) and
        /// it has to be qualified instead.
        /// </summary>
        /// <param name="semanticModel">Semantic model of the document the reference lives in.</param>
        /// <param name="position">Position the name is written at.</param>
        /// <param name="typeName">The short name of the moved type.</param>
        private bool IsShadowedByTargetNamespace(
            SemanticModel semanticModel,
            int position,
            string typeName
            )
        {
            var targetNamespace = _targetNamespaceInfo.ModifiedName;

            if (string.IsNullOrEmpty(targetNamespace) || string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            var enclosingSymbol = semanticModel.GetEnclosingSymbol(position);

            var @namespace = enclosingSymbol as INamespaceSymbol
                ?? enclosingSymbol?.ContainingNamespace
                ;

            //every namespace the reference is written in, from the innermost one to the global
            //one: `namespace A.B { }` declares the scope of `A` and the scope of `A.B`
            while (true)
            {
                var scope = @namespace == null || @namespace.IsGlobalNamespace
                    ? string.Empty
                    : @namespace.ToDisplayString()
                    ;

                //the namespace this scope will contain under the name of the type
                var candidate = scope.Length == 0
                    ? typeName
                    : scope + "." + typeName
                    ;

                if (targetNamespace == candidate
                    || targetNamespace.StartsWith(candidate + ".", StringComparison.Ordinal)
                    )
                {
                    return true;
                }

                if (@namespace == null || @namespace.IsGlobalNamespace)
                {
                    return false;
                }

                @namespace = @namespace.ContainingNamespace;
            }
        }

        /// <summary>
        /// A name we write into the file is a relative one and is resolved from the namespace
        /// that file is in: <c>X.Y.Class1</c> written inside <c>namespace Some.X</c> is
        /// <c>Some.X.Y.Class1</c> and not <c>X.Y.Class1</c>. Such a name has to be prefixed
        /// with <c>global::</c>, which is the only way to name the root namespace explicitly.
        ///
        /// The prefix is added only when it is really required: it is a noise in the code and
        /// almost no one writes it by hand.
        /// </summary>
        /// <param name="semanticModel">Semantic model of the document the name is written in.</param>
        /// <param name="position">Position the name is written at.</param>
        /// <param name="targetNamespace">The namespace the name starts with.</param>
        private static bool IsGlobalPrefixRequired(
            SemanticModel semanticModel,
            int position,
            string targetNamespace
            )
        {
            if (string.IsNullOrEmpty(targetNamespace))
            {
                return false;
            }

            var dotIndex = targetNamespace.IndexOf('.');
            var firstPart = dotIndex >= 0
                ? targetNamespace.Substring(0, dotIndex)
                : targetNamespace
                ;

            //everything which is visible at that position under the name of the first part
            //of the target namespace: anything except the root level namespace of that very
            //name shadows it and makes the written name mean something else
            foreach (var symbol in semanticModel.LookupNamespacesAndTypes(position, name: firstPart))
            {
                if (symbol is INamespaceSymbol ns && ns.ToDisplayString() == firstPart)
                {
                    continue;
                }

                return true;
            }

            return false;
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
