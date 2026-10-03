using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    /// <summary>
    /// A types separated by its namespace.
    /// </summary>
    public readonly struct NamespaceTypeContainer
    {
        /// <summary>
        /// Names of the types of the solution grouped by their containing namespace.
        ///
        /// The names and not the symbols: a file which several projects compile declares
        /// a separate type per project (a shared project, a target framework of a multi target
        /// project), and all of these types are the same type for the question this container
        /// answers.
        /// </summary>
        private readonly Dictionary<string, HashSet<string>> _dictByNamespace;

        /// <summary>
        /// The names reserved by <see cref="Reserve"/>, with the type which has reserved each
        /// of them: the other part of the same partial type reserves the same name again.
        /// </summary>
        private readonly Dictionary<(string Namespace, string Name), string> _reservedBy;

        /// <param name="unused">Not used; see the comment inside.</param>
        public NamespaceTypeContainer(
            bool unused //here is CS0568 in VS2019 without this
            )
        {
            _dictByNamespace = new Dictionary<string, HashSet<string>>(
                );
            _reservedBy = new Dictionary<(string Namespace, string Name), string>();
        }

        /// <summary>
        /// Add a type into the container.
        /// The nested types are skipped: <c>A.B.Container.Nested</c> belongs to its outer type
        /// and not to the namespace <c>A.B</c>, so it must not produce a name conflict there.
        /// The file-local types of C# 11 (<c>file class Helper</c>) are skipped as well: such a
        /// type is visible in its own file only, and the compiler accepts another type of the
        /// same name in the same namespace.
        /// </summary>
        public void Add(INamedTypeSymbol symbol)
        {
            if (symbol.ContainingType != null)
            {
                return;
            }

            if (symbol.IsFileLocal)
            {
                return;
            }

            var key = symbol.ContainingNamespace.ToDisplayString();
            if (!_dictByNamespace.TryGetValue(key, out var typeNames))
            {
                typeNames = new HashSet<string>();
                _dictByNamespace[key] = typeNames;
            }

            typeNames.Add(symbol.Name);
        }

        /// <summary>
        /// Reserve a type name in a namespace as if the type were already there.
        /// Used while collecting the subject files: two files which both move a type of
        /// the same name into the same target must conflict with each other and not only
        /// with the types which exist in the solution already.
        /// </summary>
        /// <param name="namespaceName">The target namespace.</param>
        /// <param name="typeName">The name of the moving type.</param>
        /// <param name="owner">The full name of the moving type before the move: the other
        /// part of the same partial type reserves the same name without a conflict.</param>
        public void Reserve(
            string namespaceName,
            string typeName,
            string owner
            )
        {
            if (namespaceName is null)
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }

            if (typeName is null)
            {
                throw new ArgumentNullException(nameof(typeName));
            }

            if (owner is null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (!_reservedBy.ContainsKey((namespaceName, typeName)))
            {
                _reservedBy[(namespaceName, typeName)] = owner;
            }
        }

        /// <summary>
        /// Check if the given namespace contains a type with the given name, or the name has
        /// been reserved there by a moving type.
        /// </summary>
        public bool CheckForTypeExists(
            string namespaceName,
            string typeName
            )
        {
            return ContainsExisting(namespaceName, typeName)
                || _reservedBy.ContainsKey((namespaceName, typeName));
        }

        /// <summary>
        /// Moving the given type into the given namespace collides with another type there:
        /// one which exists already, or one which has reserved the name before. This is how
        /// the name conflicts are detected before the adjusting starts.
        /// </summary>
        /// <param name="namespaceName">The target namespace.</param>
        /// <param name="typeName">The name of the moving type.</param>
        /// <param name="owner">The full name of the moving type before the move, see <see cref="Reserve"/>.</param>
        public bool IsConflict(
            string namespaceName,
            string typeName,
            string owner
            )
        {
            if (ContainsExisting(namespaceName, typeName))
            {
                return true;
            }

            return _reservedBy.TryGetValue((namespaceName, typeName), out var reservedBy)
                && reservedBy != owner;
        }

        private bool ContainsExisting(
            string namespaceName,
            string typeName
            )
        {
            return _dictByNamespace.TryGetValue(namespaceName, out var typeNames)
                && typeNames.Contains(typeName);
        }

        /// <summary>
        /// Build a container.
        /// </summary>
        /// <param name="workspace">Workspace</param>
        public static async Task<NamespaceTypeContainer> CreateForAsync(
            Workspace workspace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            var result = new NamespaceTypeContainer(false);
            foreach (var cproject in workspace.CurrentSolution.Projects)
            {
                var ccompilation = await cproject.GetCompilationAsync();
                if (ccompilation == null)
                {
                    continue;
                }

                foreach (var ctype in ccompilation.Assembly.GlobalNamespace.GetAllTypes())
                {
                    result.Add(ctype);
                }
            }

            return result;
        }

    }

    /// <summary>
    /// Type (INamedTypeSymbol) container.
    /// </summary>
    public readonly struct TypeContainer
    {
        private readonly Dictionary<string, NamedTypeExtension> _dictByFullName;

        /// <summary>
        /// Types of the solution keyed by their full names.
        /// </summary>
        public IReadOnlyDictionary<string, NamedTypeExtension> DictByFullName => _dictByFullName;

        /// <param name="unused">Not used; see the comment inside.</param>
        public TypeContainer(
            bool unused //here is CS0568 in VS2019 without this
            )
        {
            _dictByFullName = new Dictionary<string, NamedTypeExtension>();
        }

        /// <summary>
        /// Check if this type is in container.
        /// </summary>
        public bool ContainsType(string typeFullName)
        {
            return _dictByFullName.ContainsKey(typeFullName);
        }

        /// <summary>
        /// Add new type in the container. If such type is already in, it will be overwritten.
        /// </summary>
        private void Add(INamedTypeSymbol symbol, string containingNamespaceName)
        {
            if (symbol is null)
            {
                throw new ArgumentNullException(nameof(symbol));
            }

            var typeFullName = symbol.ToDisplayString();
            _dictByFullName[typeFullName] = new NamedTypeExtension(symbol, typeFullName, containingNamespaceName);
        }

        /// <summary>
        /// Build type container.
        /// </summary>
        /// <param name="workspace">Workspace</param>
        /// <param name="sourceNamespaces">Namespace list types from you are interested for. May be null for all types in the workspace.</param>
        public static async Task<TypeContainer> CreateForAsync(
            Workspace workspace,
            string[]? sourceNamespaces = null
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            var result = new TypeContainer(false);
            foreach (var cproject in workspace.CurrentSolution.Projects)
            {
                var ccompilation = await cproject.GetCompilationAsync();
                if (ccompilation == null)
                {
                    continue;
                }

                foreach (var ctype in ccompilation.Assembly.GlobalNamespace.GetAllTypes())
                {
                    var containingNamespaceName = ctype.ContainingNamespace.ToDisplayString();
                    if (sourceNamespaces == null || sourceNamespaces.Length == 0 || sourceNamespaces.Any(sn => containingNamespaceName.StartsWith(sn)))
                    {
                        result.Add(ctype, containingNamespaceName); //reuse existing value, only for performance reason
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// A type with its precalculated names (to avoid the repeated ToDisplayString calls).
    /// </summary>
    public readonly struct NamedTypeExtension
    {
        /// <summary>
        /// The type itself.
        /// </summary>
        public readonly INamedTypeSymbol Type;

        /// <summary>
        /// Full name of the type.
        /// </summary>
        public readonly string TypeFullName;

        /// <summary>
        /// Name of the containing namespace.
        /// </summary>
        public readonly string ContainingNamespaceName;

        public NamedTypeExtension(
            INamedTypeSymbol type,
            string typeFullName,
            string containingNamespaceName
            )
        {
            Type = type;
            TypeFullName = typeFullName;
            ContainingNamespaceName = containingNamespaceName;
        }
    }
}
