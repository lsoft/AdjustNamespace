using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Roslyn
{
    /// <summary>
    /// The lookup of a simple name as C# does it, for a code which is going to be written in
    /// another namespace: the semantic model answers for the place the code is written at now,
    /// and the adjusting has to know what a name of a moved file is going to mean in its new
    /// namespace.
    ///
    /// C# looks a name up in the members of every enclosing namespace, from the innermost one,
    /// before it looks into the using clauses of the file; a type of an enclosing namespace
    /// therefore wins over an imported one, and two imported types of the same name make the
    /// name ambiguous.
    /// </summary>
    public static class NameLookup
    {
        /// <summary>
        /// Look the name up in the members of the namespaces which enclose a code written in the
        /// given namespace, from the innermost one to the outermost one. The global namespace is
        /// not searched: its members are seen the same way from everywhere.
        ///
        /// The namespaces of the chain are members of each other even if they do not exist in
        /// the compilation yet (<c>Target.Place</c> makes <c>Place</c> a member of <c>Target</c>).
        /// </summary>
        /// <param name="compilation">The compilation the name is looked up in.</param>
        /// <param name="namespace">The namespace the code is written in.</param>
        /// <param name="name">The name.</param>
        /// <param name="arity">The number of the type arguments the name is written with:
        /// a namespace is found by a name without them only.</param>
        /// <param name="found">The member found; <c>null</c> for a namespace of the chain which
        /// does not exist in the compilation yet.</param>
        /// <returns><c>false</c> if none of these namespaces has such a member.</returns>
        public static bool TryFindInEnclosingNamespaces(
            Compilation compilation,
            string @namespace,
            string name,
            int arity,
            out ISymbol? found
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            found = null;

            if (string.IsNullOrEmpty(@namespace) || string.IsNullOrEmpty(name))
            {
                return false;
            }

            var parts = @namespace.Split('.');

            for (var level = parts.Length; level >= 1; level--)
            {
                var levelNamespace = compilation.TryFindNamespace(string.Join(".", parts, 0, level));

                if (arity == 0)
                {
                    var childNamespace = levelNamespace?
                        .GetNamespaceMembers()
                        .FirstOrDefault(n => n.Name == name)
                        ;

                    if (childNamespace != null)
                    {
                        found = childNamespace;
                        return true;
                    }

                    if (level < parts.Length && parts[level] == name)
                    {
                        //the next namespace of the chain, which is going to exist
                        return true;
                    }
                }

                var type = levelNamespace?
                    .GetTypeMembers(name, arity)
                    .FirstOrDefault(t => IsVisibleOutsideItsFile(compilation, t))
                    ;

                if (type != null)
                {
                    found = type;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The name, looked up from a code written in the given namespace, does not reach the
        /// root namespace of that name: something of the enclosing namespaces hides it, and
        /// only <c>global::</c> names it.
        /// </summary>
        public static bool IsRootNamespaceHidden(
            Compilation compilation,
            string @namespace,
            string rootNamespaceName
            )
        {
            return TryFindInEnclosingNamespaces(compilation, @namespace, rootNamespaceName, 0, out _);
        }

        /// <summary>
        /// The types of the given name the using clauses at the given position import, plus the
        /// ones the given namespaces would import if they were imported there too.
        /// </summary>
        /// <param name="semanticModel">Semantic model of the document.</param>
        /// <param name="position">Position the name is written at.</param>
        /// <param name="additionalNamespaces">The namespaces whose using clauses are going to be added.</param>
        /// <param name="name">The name.</param>
        /// <param name="arity">The number of the type arguments the name is written with.</param>
        public static List<INamedTypeSymbol> FindImportedTypes(
            SemanticModel semanticModel,
            int position,
            IEnumerable<string> additionalNamespaces,
            string name,
            int arity
            )
        {
            if (semanticModel is null)
            {
                throw new ArgumentNullException(nameof(semanticModel));
            }

            if (additionalNamespaces is null)
            {
                throw new ArgumentNullException(nameof(additionalNamespaces));
            }

            var compilation = semanticModel.Compilation;
            var containers = new List<INamespaceOrTypeSymbol>();

            foreach (var scope in semanticModel.GetImportScopes(position))
            {
                foreach (var import in scope.Imports)
                {
                    containers.Add(import.NamespaceOrType);
                }
            }

            foreach (var additionalNamespace in additionalNamespaces)
            {
                var @namespace = compilation.TryFindNamespace(additionalNamespace);
                if (@namespace != null)
                {
                    containers.Add(@namespace);
                }
            }

            var result = new List<INamedTypeSymbol>();

            foreach (var container in containers)
            {
                foreach (var type in container.GetTypeMembers(name, arity))
                {
                    if (!IsVisibleOutsideItsFile(compilation, type))
                    {
                        continue;
                    }

                    if (result.Contains(type, SymbolEqualityComparer.Default))
                    {
                        continue;
                    }

                    result.Add(type);
                }
            }

            return result;
        }

        /// <summary>
        /// The type may be found by a name written in another file of the compilation:
        /// it is accessible, and it is no file-local type of C# 11 (<c>file class Helper</c>),
        /// which is visible in its own file only.
        /// </summary>
        public static bool IsVisibleOutsideItsFile(Compilation compilation, INamedTypeSymbol type)
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            return !type.IsFileLocal
                && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
        }

        /// <summary>
        /// The two symbols are the same type (or the same namespace), whatever their type
        /// arguments are.
        /// </summary>
        public static bool IsSame(ISymbol? left, ISymbol? right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (left is INamespaceSymbol leftNamespace && right is INamespaceSymbol rightNamespace)
            {
                return leftNamespace.ToDisplayString() == rightNamespace.ToDisplayString();
            }

            return SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, right.OriginalDefinition);
        }
    }
}
