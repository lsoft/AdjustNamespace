using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Namespace;
using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using AdjustNamespace;

namespace AdjustNamespace.Adjusting.Adjuster.Cs
{
    /// <summary>
    /// Fixer of the references the adjusted file itself makes to other types.
    ///
    /// A file may reference a type without a <c>using</c> clause, relying on being nested
    /// inside that type's namespace (<c>Some.A.B</c> sees <c>Some.A</c> unqualified, because
    /// a name is looked up in every namespace enclosing it before the using clauses are even
    /// considered). Moving the file's own namespace declaration may take it out of that
    /// enclosing namespace, and such a reference is not among the ones <see cref="RefProcessor"/>
    /// fixes: that class only follows the references TO the types declared in the file being
    /// moved, never the references the file itself makes to types declared elsewhere.
    ///
    /// The same lookup applies to extension methods of the enclosing namespace
    /// (<c>value.Twice()</c>): the call is written as a member access, so it looks
    /// "already qualified", but the method itself is found only because the enclosing
    /// namespace is searched.
    /// </summary>
    public readonly struct SelfReferenceFixer
    {
        private readonly EditSet _edits;
        private readonly string _subjectFilePath;
        private readonly string _targetNamespace;

        /// <param name="edits">Set the scheduled edits are placed into.</param>
        /// <param name="subjectFilePath">The file being adjusted.</param>
        /// <param name="targetNamespace">Target namespace of that file.</param>
        public SelfReferenceFixer(
            EditSet edits,
            string subjectFilePath,
            string targetNamespace
            )
        {
            if (edits is null)
            {
                throw new ArgumentNullException(nameof(edits));
            }

            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            if (targetNamespace is null)
            {
                throw new ArgumentNullException(nameof(targetNamespace));
            }

            _edits = edits;
            _subjectFilePath = subjectFilePath;
            _targetNamespace = targetNamespace;
        }

        /// <summary>
        /// Walk every simple name of the given tree (plus every operator and indexer use,
        /// which may be an extension member of C# 14 / 15 written with no name at all) and
        /// schedule a <c>using</c> clause for the ones which stop resolving once the file's
        /// own namespace is moved.
        /// </summary>
        /// <param name="syntaxRoot">Syntax root of the subject file (one of its trees).</param>
        /// <param name="semanticModel">Semantic model of that very tree.</param>
        public void Fix(
            SyntaxNode syntaxRoot,
            SemanticModel semanticModel
            )
        {
            if (syntaxRoot is null)
            {
                throw new ArgumentNullException(nameof(syntaxRoot));
            }

            if (semanticModel is null)
            {
                throw new ArgumentNullException(nameof(semanticModel));
            }

            QualifyNamespaceHeads(syntaxRoot, semanticModel);

            var addedUsings = new HashSet<string>(StringComparer.Ordinal);

            foreach (var node in syntaxRoot.DescendantNodes().Where(IsCandidate))
            {
                if (!TryGetImportedNamespace(node, semanticModel, out var symbolNamespace, out var declaredInSubjectFile))
                {
                    continue;
                }

                if (declaredInSubjectFile)
                {
                    //this type (or the class of this extension method) moves together
                    //with the file, no using is needed for it
                    continue;
                }

                if (string.IsNullOrEmpty(symbolNamespace))
                {
                    continue;
                }

                var transition = NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf(
                    node,
                    _targetNamespace
                    );
                if (!transition.HasValue)
                {
                    //the name is not written inside a namespace declaration which moves
                    continue;
                }

                if (!IsNestedIn(transition.Value.OriginalName, symbolNamespace))
                {
                    //this reference did not rely on the enclosing namespace to begin with
                    //(it is covered by an existing using clause, for example)
                    continue;
                }

                if (IsNestedIn(transition.Value.ModifiedName, symbolNamespace))
                {
                    //still nested inside that namespace after the move
                    continue;
                }

                AdjustLog.WriteLine(
                    $"[Adjust] SelfReferenceFixer: {_subjectFilePath}: '{node}' relies on the enclosing "
                    + $"namespace {transition.Value.OriginalName} -> add using {symbolNamespace}"
                    );

                _edits.AddUsing(_subjectFilePath, symbolNamespace);
                addedUsings.Add(symbolNamespace);
            }

            foreach (var edit in _edits.EditsOf(_subjectFilePath).OfType<AddUsingEdit>())
            {
                addedUsings.Add(edit.NamespaceName);
            }

            QualifyRebindingTypeNames(syntaxRoot, semanticModel, addedUsings);
        }

        /// <summary>
        /// A namespace written as the head of a name (<c>Fourth</c> of <c>Fourth.Thing</c>, of
        /// <c>using Fourth;</c> inside the namespace declaration) is resolved from the namespace
        /// the file is in, and it may mean another namespace, or nothing, once the file is moved:
        /// <list type="bullet">
        /// <item>a child namespace of an enclosing namespace (<c>Properties.Settings.Default</c>
        /// inside <c>Legacy.Forms</c>, where <c>Properties</c> is <c>Legacy.Properties</c>) is not
        /// found once the file leaves that enclosing namespace, and a using clause does not help:
        /// it imports the types of a namespace and never its child namespaces;</item>
        /// <item>a root namespace (<c>First</c> of <c>First.Second.Thing</c>) is hidden by a
        /// namespace or a type of that name the new namespaces of the file contain
        /// (the file moves into <c>Target.First</c>).</item>
        /// </list>
        /// Such a head is written out as the full name of the namespace, with <c>global::</c>
        /// when the root of it is hidden too.
        /// </summary>
        private void QualifyNamespaceHeads(
            SyntaxNode syntaxRoot,
            SemanticModel semanticModel
            )
        {
            var compilation = semanticModel.Compilation;

            foreach (var name in syntaxRoot.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!IsHeadOfDottedName(name) && !(name.Parent is UsingDirectiveSyntax))
                {
                    continue;
                }

                if (IsInNamespaceDeclarationName(name))
                {
                    continue;
                }

                if (!(semanticModel.GetSymbolInfo(name).Symbol is INamespaceSymbol @namespace)
                    || @namespace.IsGlobalNamespace
                    )
                {
                    continue;
                }

                if (semanticModel.GetAliasInfo(name) != null)
                {
                    //an alias is declared by the file and means the same after the move
                    continue;
                }

                if (NamesTypeOfSubjectFile(name, semanticModel))
                {
                    //the whole name is rewritten by RefProcessor, as a reference to a moved type
                    continue;
                }

                var transition = NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf(
                    name,
                    _targetNamespace
                    );
                if (!transition.HasValue)
                {
                    continue;
                }

                var fullName = @namespace.ToDisplayString();
                var writtenName = name.Identifier.ValueText;
                var modifiedName = transition.Value.ModifiedName;

                if (NameLookup.TryFindInEnclosingNamespaces(compilation, modifiedName, writtenName, 0, out var found))
                {
                    if (NameLookup.IsSame(found, @namespace))
                    {
                        //the new namespaces of the file contain the very same namespace
                        continue;
                    }
                }
                else if (fullName == writtenName)
                {
                    //a root namespace, and nothing hides it
                    continue;
                }

                var qualified = (NameLookup.IsRootNamespaceHidden(compilation, modifiedName, fullName.Split('.')[0]) ? "global::" : string.Empty)
                    + fullName;

                AdjustLog.WriteLine(
                    $"[Adjust] SelfReferenceFixer: {_subjectFilePath}: '{name.Parent}' does not resolve to "
                    + $"{fullName} inside {modifiedName} -> write '{writtenName}' as '{qualified}'"
                    );

                _edits.ReplaceText(_subjectFilePath, name.Span, qualified);
            }
        }

        /// <summary>
        /// A type written by its simple name may mean another type once the file is moved, or
        /// nothing at all:
        /// <list type="bullet">
        /// <item>the new namespaces of the file contain a type of that name, which wins over
        /// the imported one and over the one of the old enclosing namespace: the name silently
        /// means another type;</item>
        /// <item>a type of the old enclosing namespace won over an imported type of the same
        /// name; once it is imported by a using clause too, the name is ambiguous (CS0104).</item>
        /// </list>
        /// Such a name is written with the namespace of its type in front of it.
        /// </summary>
        /// <param name="syntaxRoot">Syntax root of the subject file.</param>
        /// <param name="semanticModel">Semantic model of that very tree.</param>
        /// <param name="addedUsings">The namespaces the file is going to import additionally.</param>
        private void QualifyRebindingTypeNames(
            SyntaxNode syntaxRoot,
            SemanticModel semanticModel,
            IReadOnlyCollection<string> addedUsings
            )
        {
            var compilation = semanticModel.Compilation;

            foreach (var name in syntaxRoot.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (IsAlreadyQualified(name)
                    || name.Parent is AliasQualifiedNameSyntax
                    || name.Ancestors().Any(a => a is UsingDirectiveSyntax)
                    || IsInNamespaceDeclarationName(name)
                    )
                {
                    continue;
                }

                var type = TypeOf(semanticModel.GetSymbolInfo(name).Symbol, name);
                if (type == null
                    || type.TypeKind == TypeKind.Error
                    || type.ContainingType != null
                    || type.ContainingNamespace == null
                    || type.ContainingNamespace.IsGlobalNamespace
                    || IsDeclaredInSubjectFile(type)
                    )
                {
                    continue;
                }

                if (semanticModel.GetAliasInfo(name) != null)
                {
                    continue;
                }

                var transition = NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf(
                    name,
                    _targetNamespace
                    );
                if (!transition.HasValue)
                {
                    continue;
                }

                var modifiedName = transition.Value.ModifiedName;
                var arity = name is GenericNameSyntax generic ? generic.Arity : 0;

                string reason;
                if (NameLookup.TryFindInEnclosingNamespaces(compilation, modifiedName, type.Name, arity, out var found))
                {
                    if (NameLookup.IsSame(found, type))
                    {
                        continue;
                    }

                    reason = $"{(found == null ? "a namespace" : found.ToDisplayString())} hides it inside {modifiedName}";
                }
                else
                {
                    var imported = NameLookup.FindImportedTypes(
                        semanticModel,
                        name.SpanStart,
                        addedUsings,
                        type.Name,
                        arity
                        );

                    var others = imported.Where(t => !NameLookup.IsSame(t, type)).ToList();
                    if (others.Count == 0)
                    {
                        continue;
                    }

                    reason = $"it is ambiguous with {string.Join(", ", others.Select(o => o.ToDisplayString()))} inside {modifiedName}";
                }

                var typeNamespace = type.ContainingNamespace.ToDisplayString();
                var prefix = (NameLookup.IsRootNamespaceHidden(compilation, modifiedName, typeNamespace.Split('.')[0]) ? "global::" : string.Empty)
                    + typeNamespace
                    + ".";

                AdjustLog.WriteLine(
                    $"[Adjust] SelfReferenceFixer: {_subjectFilePath}: '{name}' means {type.ToDisplayString()}, but {reason} -> write it as '{prefix}{name.Identifier.Text}'"
                    );

                _edits.ReplaceText(_subjectFilePath, name.Identifier.Span, prefix + name.Identifier.Text);
            }
        }

        /// <summary>
        /// The type a name means: the type itself, or the type of the constructor an attribute
        /// name means.
        /// </summary>
        private static INamedTypeSymbol? TypeOf(ISymbol? symbol, SimpleNameSyntax name)
        {
            if (symbol is INamedTypeSymbol type)
            {
                return type;
            }

            if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
                && name.Parent is AttributeSyntax
                )
            {
                return constructor.ContainingType;
            }

            return null;
        }

        /// <summary>
        /// The dotted name the given head starts reaches a type declared in the subject file
        /// (<c>First.Second.Third.MyClass</c> inside the file of <c>MyClass</c>).
        /// </summary>
        private bool NamesTypeOfSubjectFile(IdentifierNameSyntax head, SemanticModel semanticModel)
        {
            SyntaxNode current = head;

            while (true)
            {
                SimpleNameSyntax? next = current.Parent switch
                {
                    QualifiedNameSyntax qualified when ReferenceEquals(qualified.Left, current) => qualified.Right,
                    MemberAccessExpressionSyntax access when ReferenceEquals(access.Expression, current) => access.Name,
                    _ => null
                };

                if (next == null)
                {
                    return false;
                }

                if (semanticModel.GetSymbolInfo(next).Symbol is INamedTypeSymbol type
                    && IsDeclaredInSubjectFile(type)
                    )
                {
                    return true;
                }

                current = current.Parent!;
            }
        }

        /// <summary>
        /// The name is a part of the name of a namespace declaration, which the adjusting
        /// moves as a whole.
        /// </summary>
        private static bool IsInNamespaceDeclarationName(SyntaxNode name)
        {
            return name.Ancestors()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .Any(declaration => declaration.Name.Span.Contains(name.Span));
        }

        /// <summary>
        /// The name is the leftmost part of a dotted name (<c>A</c> of <c>A.B.C</c>), written as
        /// a qualified name or as a member access.
        /// </summary>
        private static bool IsHeadOfDottedName(IdentifierNameSyntax name)
        {
            if (name.Parent is QualifiedNameSyntax qualified)
            {
                return ReferenceEquals(qualified.Left, name);
            }

            if (name.Parent is MemberAccessExpressionSyntax memberAccess)
            {
                return ReferenceEquals(memberAccess.Expression, name);
            }

            return false;
        }

        /// <summary>
        /// A node which may rely on an enclosing namespace: a simple name, or a use of an
        /// operator or an indexer, which may be an extension member written with no name.
        /// </summary>
        private static bool IsCandidate(SyntaxNode node)
        {
            return node is SimpleNameSyntax
                || node is BinaryExpressionSyntax
                || node is PrefixUnaryExpressionSyntax
                || node is PostfixUnaryExpressionSyntax
                || node is AssignmentExpressionSyntax
                || node is ElementAccessExpressionSyntax
                ;
        }

        /// <summary>
        /// The namespace a node relies on to resolve, if any: a bare type name, an extension
        /// method or an extension block member invoked as a member access
        /// (<c>receiver.Method()</c>, <c>receiver.Property</c>, <c>Type.StaticMember()</c>),
        /// or an extension operator or indexer (<c>a + b</c>, <c>receiver[0]</c>).
        /// </summary>
        private bool TryGetImportedNamespace(
            SyntaxNode node,
            SemanticModel semanticModel,
            out string symbolNamespace,
            out bool declaredInSubjectFile
            )
        {
            symbolNamespace = string.Empty;
            declaredInSubjectFile = false;

            var symbol = semanticModel.GetSymbolInfo(node).Symbol;
            if (symbol == null)
            {
                return false;
            }

            if (node is SimpleNameSyntax nameSyntax)
            {
                if (symbol is INamedTypeSymbol typeSymbol)
                {
                    if (IsAlreadyQualified(nameSyntax))
                    {
                        //this name spells its namespace out already and does not depend
                        //on the enclosing namespace at all
                        return false;
                    }

                    return TryFromType(typeSymbol, out symbolNamespace, out declaredInSubjectFile);
                }

                //an extension member is written as a member access (`value.Twice()`,
                //`value.Loud`, `Cat.Create()`): the name looks qualified, but the member is
                //found only because the enclosing namespaces are searched for extensions
                if (!IsMemberAccessName(nameSyntax))
                {
                    return false;
                }
            }
            else if (!(symbol.ContainingType?.IsExtensionBlock() ?? false))
            {
                //an operator or an indexer of a type itself needs no import at all
                return false;
            }

            var container = symbol.TryGetImportedExtensionContainer();
            if (container == null)
            {
                return false;
            }

            return TryFromType(container, out symbolNamespace, out declaredInSubjectFile);
        }

        private bool TryFromType(
            INamedTypeSymbol typeSymbol,
            out string symbolNamespace,
            out bool declaredInSubjectFile
            )
        {
            declaredInSubjectFile = IsDeclaredInSubjectFile(typeSymbol);

            var containingNamespace = typeSymbol.ContainingNamespace;
            if (containingNamespace == null || containingNamespace.IsGlobalNamespace)
            {
                symbolNamespace = string.Empty;
                return false;
            }

            symbolNamespace = containingNamespace.ToDisplayString();
            return true;
        }

        /// <summary>
        /// A qualified name (<c>A.B.Class1</c>) or a member access (<c>A.B.Class1.Member</c>)
        /// already spells its namespace out, so only the head of such a chain (or a name
        /// written on its own) is a candidate for the enclosing-namespace lookup of a type.
        /// </summary>
        private static bool IsAlreadyQualified(
            SimpleNameSyntax nameSyntax
            )
        {
            if (nameSyntax.Parent is QualifiedNameSyntax qns
                && ReferenceEquals(qns.Right, nameSyntax)
                )
            {
                return true;
            }

            return IsMemberAccessName(nameSyntax);
        }

        private static bool IsMemberAccessName(
            SimpleNameSyntax nameSyntax
            )
        {
            return nameSyntax.Parent is MemberAccessExpressionSyntax maes
                && ReferenceEquals(maes.Name, nameSyntax)
                ;
        }

        private bool IsDeclaredInSubjectFile(
            INamedTypeSymbol symbol
            )
        {
            var subjectFilePath = _subjectFilePath;

            return symbol.DeclaringSyntaxReferences
                .Any(r => string.Equals(
                    r.SyntaxTree.FilePath,
                    subjectFilePath,
                    StringComparison.OrdinalIgnoreCase
                    ));
        }

        /// <summary>
        /// Whether <paramref name="scope"/> is <paramref name="namespaceName"/> itself or
        /// a namespace nested inside it, i.e. whether the members of
        /// <paramref name="namespaceName"/> are visible unqualified from <paramref name="scope"/>.
        /// </summary>
        private static bool IsNestedIn(
            string scope,
            string namespaceName
            )
        {
            return scope == namespaceName
                || scope.StartsWith(namespaceName + ".", StringComparison.Ordinal)
                ;
        }
    }
}
