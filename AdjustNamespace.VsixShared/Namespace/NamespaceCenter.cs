using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// Namespace state container. It accumulates a changes during adjusting.
    /// </summary>
    public class NamespaceCenter
    {
        /// <summary>
        /// Types (full names) of the solution grouped by their containing namespace.
        /// A type is removed from here as soon as it has been moved into another namespace.
        /// </summary>
        private readonly Dictionary<string, HashSet<string>> _types;

        /// <summary>
        /// Emptyfied namespaces. They need to be removed at the end of adjusting procedure.
        /// </summary>
        private readonly HashSet<string> _namespacesToRemove;

        private NamespaceCenter(
            Dictionary<string, HashSet<string>> types
            )
        {
            if (types is null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            _types = types;
            _namespacesToRemove = new HashSet<string>();
        }

        /// <summary>
        /// Filter incoming namespaces and return only those are allowed to delete
        /// (namespaces that does not exists after adjusting).
        /// </summary>
        /// <param name="namespacesToCheck">Using clauses of a document.</param>
        /// <returns>Those of them which point to an emptied namespace.</returns>
        public List<SyntaxNode> GetRemovedNamespaces(
            IReadOnlyList<UsingDirectiveSyntax> namespacesToCheck
            )
        {
            if (namespacesToCheck is null)
            {
                throw new ArgumentNullException(nameof(namespacesToCheck));
            }

            if (namespacesToCheck.Count == 0)
            {
                return new List<SyntaxNode>();
            }

            var toRemove = new List<SyntaxNode>(namespacesToCheck.Count);

            foreach (var n in namespacesToCheck)
            {
                var nname = n.Name.ToString();

                if (!_namespacesToRemove.Contains(nname))
                {
                    //there are a types in this namespace
                    continue;
                }

                toRemove.Add(n);
            }

            return toRemove;
        }

        /// <summary>
        /// Report that the given type has been moved out of its namespace.
        /// If it was the last type of that namespace, the namespace is marked
        /// as the subject to remove from the using clauses.
        /// </summary>
        public void TypeRemoved(ITypeSymbol type)
        {
            var cnn = type.ContainingNamespace.ToDisplayString();
            if (!_types.TryGetValue(cnn, out var set))
            {
                return;
            }

            var tn = type.ToDisplayString();

            if (!set.Contains(tn))
            {
                return;
            }

            set.Remove(tn);

            if (set.Count == 0)
            {
                _namespacesToRemove.Add(cnn);
            }
        }

        /// <summary>
        /// Build a namespace center for a whole workspace.
        /// </summary>
        public static async Task<NamespaceCenter> CreateForAsync(
            VisualStudioWorkspace workspace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            var typeContainer = await TypeContainer.CreateForAsync(workspace);

            var types = new Dictionary<string, HashSet<string>>(typeContainer.DictByFullName.Count);
            foreach (var nte in typeContainer.DictByFullName.Values)
            {
                var key = nte.ContainingNamespaceName;
                if (!types.ContainsKey(key))
                {
                    types[key] = new HashSet<string>();
                }
                types[key].Add(nte.TypeFullName);
            }

            var result = new NamespaceCenter(types);
            return result;
        }

    }
}
