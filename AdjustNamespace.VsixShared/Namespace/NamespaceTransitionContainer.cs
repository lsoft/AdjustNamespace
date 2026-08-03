using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdjustNamespace.Namespace
{
    /// <summary>
    /// A collection for namespace transitions.
    /// </summary>
    public readonly struct NamespaceTransitionContainer
    {
        /// <summary>
        /// All the transitions of a document.
        /// </summary>
        public readonly IReadOnlyList<NamespaceTransition> Transitions;

        /// <summary>
        /// The same transitions, keyed by the original namespace name.
        /// </summary>
        public readonly IReadOnlyDictionary<string, NamespaceTransition> TransitionDict;

        /// <summary>
        /// There is nothing to change in the document.
        /// </summary>
        public readonly bool IsEmpty;

        public NamespaceTransitionContainer(
            List<NamespaceTransition> transitions
            )
        {
            if (transitions is null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            Transitions = transitions;
            TransitionDict = BuildTransitionDict(transitions);

            IsEmpty = transitions.Count == 0;
        }

        private static Dictionary<string, NamespaceTransition> BuildTransitionDict(
            List<NamespaceTransition> transitions
            )
        {
            var transitionDict = new Dictionary<string, NamespaceTransition>(transitions.Count);
            foreach (var info in transitions)
            {
                var key = info.OriginalName;
                if (!transitionDict.ContainsKey(key))
                {
                    transitionDict[key] = info;
                }
            }

            return transitionDict;
        }


        /// <summary>
        /// Build the transitions for every namespace declaration found in the document.
        /// The namespaces which are in the target namespace already and the special ones
        /// (see <see cref="NamespaceHelper.IsSpecialNamespace"/>) are not included.
        /// </summary>
        /// <param name="node">Syntax root of the document.</param>
        /// <param name="root">Target namespace for that document.</param>
        public static NamespaceTransitionContainer GetNamespaceTransitionsFor(
            SyntaxNode node,
            string root
            )
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }


            var candidateNamespaces = (
                from dnode in node.DescendantNodesAndSelf()
                let tdnode = dnode as NamespaceDeclarationSyntax
                where tdnode != null
                let ni = TryGetNamespaceTransitionInfo(tdnode, root)
                where ni.HasValue
                where !NamespaceHelper.IsSpecialNamespace(ni.Value.OriginalName)
                select ni.Value
                ).ToList();

#if VS2022
            var candidateNamespaces2 = (
                from dnode in node.DescendantNodesAndSelf()
                let fsndnode = dnode as FileScopedNamespaceDeclarationSyntax
                where fsndnode != null
                let ni = TryGetNamespaceTransitionInfo(fsndnode, root)
                where ni.HasValue
                where !NamespaceHelper.IsSpecialNamespace(ni.Value.OriginalName)
                select ni.Value
                ).ToList();

            candidateNamespaces.AddRange(candidateNamespaces2);
#endif

            return new NamespaceTransitionContainer(candidateNamespaces);

        }

#if VS2022

        /// <summary>
        /// Build a transition for a file scoped namespace declaration (<c>namespace A;</c>).
        /// Such a declaration cannot be nested, so its name is always the full one.
        /// </summary>
        /// <returns><c>null</c> if the namespace is the target one already.</returns>
        public static NamespaceTransition? TryGetNamespaceTransitionInfo(
            FileScopedNamespaceDeclarationSyntax n,
            string root
            )
        {
            if (n is null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var originalNamespace = n.Name.ToString();
            var clonedNamespace = root;

            if (originalNamespace == clonedNamespace)
            {
                return null;
            }

            return new NamespaceTransition(
                originalNamespace,
                clonedNamespace,
                true
                );
        }

#endif

        /// <summary>
        /// Build a transition for a classic namespace declaration (<c>namespace A { }</c>).
        /// Such a declaration may be nested (<c>namespace A { namespace B { } }</c>),
        /// so the full name is collected by walking up the syntax tree, and only
        /// the outermost part of the name is replaced with the target namespace.
        /// </summary>
        /// <returns><c>null</c> if the namespace is the target one already.</returns>
        private static NamespaceTransition? TryGetNamespaceTransitionInfo(
            NamespaceDeclarationSyntax n,
            string root
            )
        {
            if (n is null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var res = new List<string>();

            SyntaxNode? p = n;
            while (p != null)
            {
                if (p is NamespaceDeclarationSyntax nds)
                {
                    res.Add(nds.Name.ToString());
                }

                p = p.Parent;
            }

            res.Reverse();

            var cloned = new List<string>(res);
            if (!string.IsNullOrEmpty(root))
            {
                cloned[0] = root!;
            }

            var originalNamespace = string.Join(".", res);
            var clonedNamespace = string.Join(".", cloned);

            if (originalNamespace == clonedNamespace)
            {
                return null;
            }

            return new NamespaceTransition(
                originalNamespace,
                clonedNamespace,
                res.Count == 1
                );
        }

    }
}
