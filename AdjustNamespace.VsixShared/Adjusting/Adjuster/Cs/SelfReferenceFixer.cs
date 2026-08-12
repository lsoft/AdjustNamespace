using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Namespace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Diagnostics;
using System.Linq;

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
        /// Walk every simple name of the given tree and schedule a <c>using</c> clause
        /// for the ones which stop resolving once the file's own namespace is moved.
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

            foreach (var nameSyntax in syntaxRoot.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (IsAlreadyQualified(nameSyntax))
                {
                    //this name spells its namespace out already and does not depend
                    //on the enclosing namespace at all
                    continue;
                }

                if (!(semanticModel.GetSymbolInfo(nameSyntax).Symbol is INamedTypeSymbol symbol))
                {
                    continue;
                }

                if (IsDeclaredInSubjectFile(symbol))
                {
                    //this type moves together with the file, no using is needed for it
                    continue;
                }

                var containingNamespace = symbol.ContainingNamespace;
                if (containingNamespace == null || containingNamespace.IsGlobalNamespace)
                {
                    continue;
                }

                var symbolNamespace = containingNamespace.ToDisplayString();

                var transition = NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf(
                    nameSyntax,
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

                Debug.WriteLine(
                    $"[Adjust] SelfReferenceFixer: {_subjectFilePath}: '{nameSyntax}' relies on the enclosing "
                    + $"namespace {transition.Value.OriginalName} -> add using {symbolNamespace}"
                    );

                _edits.AddUsing(_subjectFilePath, symbolNamespace);
            }
        }

        /// <summary>
        /// A qualified name (<c>A.B.Class1</c>) or a member access (<c>A.B.Class1.Member</c>)
        /// already spells its namespace out, so only the head of such a chain (or a name
        /// written on its own) is a candidate for the enclosing-namespace lookup.
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

            if (nameSyntax.Parent is MemberAccessExpressionSyntax maes
                && ReferenceEquals(maes.Name, nameSyntax)
                )
            {
                return true;
            }

            return false;
        }

        private bool IsDeclaredInSubjectFile(
            INamedTypeSymbol symbol
            )
        {
            var subjectFilePath = _subjectFilePath;

            return symbol.DeclaringSyntaxReferences
                .Any(r => r.SyntaxTree.FilePath == subjectFilePath);
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
