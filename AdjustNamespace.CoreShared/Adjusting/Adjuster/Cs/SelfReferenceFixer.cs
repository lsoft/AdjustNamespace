using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Namespace;
using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
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

            QualifyChildNamespaceHeads(syntaxRoot, semanticModel);

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
            }
        }

        /// <summary>
        /// A name written through a child namespace of an enclosing namespace
        /// (<c>Properties.Settings.Default</c> inside <c>Legacy.Forms</c>, where <c>Properties</c>
        /// is <c>Legacy.Properties</c>) resolves only because the file is nested inside that
        /// enclosing namespace. Once the file leaves it, the head of the name has to be written
        /// out: a using clause imports the types of a namespace and never its child namespaces.
        /// </summary>
        private void QualifyChildNamespaceHeads(
            SyntaxNode syntaxRoot,
            SemanticModel semanticModel
            )
        {
            foreach (var name in syntaxRoot.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!IsHeadOfDottedName(name))
                {
                    continue;
                }

                if (!(semanticModel.GetSymbolInfo(name).Symbol is INamespaceSymbol @namespace)
                    || @namespace.IsGlobalNamespace
                    )
                {
                    continue;
                }

                var fullName = @namespace.ToDisplayString();
                var writtenName = name.Identifier.ValueText;

                if (fullName == writtenName)
                {
                    //a root level namespace resolves from everywhere
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

                if (!fullName.EndsWith("." + writtenName, StringComparison.Ordinal))
                {
                    //found through an alias or a using clause, not through an enclosing namespace
                    continue;
                }

                var parentName = fullName.Substring(0, fullName.Length - writtenName.Length - 1);

                if (!IsNestedIn(transition.Value.OriginalName, parentName))
                {
                    //the parent is no enclosing namespace of the file: the name does not rely on it
                    continue;
                }

                if (IsNestedIn(transition.Value.ModifiedName, parentName))
                {
                    //still nested inside that namespace after the move
                    continue;
                }

                var firstPart = fullName.Split('.')[0];
                var isGlobalPrefixRequired = transition.Value.ModifiedName
                    .Split('.')
                    .Contains(firstPart);

                var qualified = (isGlobalPrefixRequired ? "global::" : string.Empty) + fullName;

                AdjustLog.WriteLine(
                    $"[Adjust] SelfReferenceFixer: {_subjectFilePath}: '{name.Parent}' relies on the enclosing "
                    + $"namespace {parentName} -> write '{writtenName}' as '{qualified}'"
                    );

                _edits.ReplaceText(_subjectFilePath, name.Span, qualified);
            }
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
