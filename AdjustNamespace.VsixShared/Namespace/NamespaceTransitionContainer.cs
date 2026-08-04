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

            var distinctTransitions = Distinct(transitions);

            Transitions = distinctTransitions;
            TransitionDict = BuildTransitionDict(distinctTransitions);

            IsEmpty = distinctTransitions.Count == 0;
        }

        /// <summary>
        /// Remove the equal transitions: the same namespace may be declared several times
        /// in a single file (<c>namespace A.B { } namespace A.B { }</c>) and every declaration
        /// produces its own transition, while such a namespace has to be moved once.
        /// </summary>
        private static List<NamespaceTransition> Distinct(
            List<NamespaceTransition> transitions
            )
        {
            var result = new List<NamespaceTransition>(transitions.Count);
            var seen = new HashSet<(string, string, bool)>();

            foreach (var info in transitions)
            {
                if (seen.Add((info.OriginalName, info.ModifiedName, info.IsRoot)))
                {
                    result.Add(info);
                }
            }

            return result;
        }

        /// <summary>
        /// Key the transitions by the original namespace name.
        /// </summary>
        /// <remarks>
        /// A namespace may still have two different transitions
        /// (<c>namespace A { namespace B { } } </c> plus <c>namespace A.B { }</c> in one file
        /// give <c>A.B -> X.Y.B</c> and <c>A.B -> X.Y</c>), which the dictionary cannot express;
        /// the first one wins there.
        /// </remarks>
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

            return new NamespaceTransitionContainer(
                CollectTransitions(node, root)
                );
        }

        /// <summary>
        /// The same for all the syntax trees of a single file: a file which is compiled by
        /// several projects has a tree per project, and a namespace declaration which is
        /// guarded by a conditional compilation symbol exists in a part of them only,
        /// see <see cref="Helper.WorkspaceHelper.GetDocuments"/>.
        /// </summary>
        /// <param name="nodes">Syntax roots of all the documents of the file.</param>
        /// <param name="root">Target namespace for that file.</param>
        public static NamespaceTransitionContainer GetNamespaceTransitionsFor(
            IReadOnlyList<SyntaxNode> nodes,
            string root
            )
        {
            if (nodes is null)
            {
                throw new ArgumentNullException(nameof(nodes));
            }

            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var transitions = new List<NamespaceTransition>();

            foreach (var node in nodes)
            {
                transitions.AddRange(CollectTransitions(node, root));
            }

            return new NamespaceTransitionContainer(transitions);
        }

        private static List<NamespaceTransition> CollectTransitions(
            SyntaxNode node,
            string root
            )
        {
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

            return candidateNamespaces;
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
