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
        public readonly IReadOnlyList<NamespaceTransition> Transitions;
        public readonly IReadOnlyDictionary<string, NamespaceTransition> TransitionDict;

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
                select ni.Value
                ).ToList();

#if VS2022
            var candidateNamespaces2 = (
                from dnode in node.DescendantNodesAndSelf()
                let fsndnode = dnode as FileScopedNamespaceDeclarationSyntax
                where fsndnode != null
                let ni = TryGetNamespaceTransitionInfo(fsndnode, root)
                where ni.HasValue
                select ni.Value
                ).ToList();

            candidateNamespaces.AddRange(candidateNamespaces2);
#endif

            return new NamespaceTransitionContainer(candidateNamespaces);

        }

#if VS2022

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
