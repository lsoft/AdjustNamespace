using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Threading;
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
        private readonly Dictionary<string, List<INamedTypeSymbol>> _dictByNamespace;

        public NamespaceTypeContainer(
            bool unused //here is CS0568 in VS2019 without this
            )
        {
            _dictByNamespace = new Dictionary<string, List<INamedTypeSymbol>>(
                );
        }

        public void Add(INamedTypeSymbol symbol)
        {
            var key = symbol.ContainingNamespace.ToFullDisplayString();
            if (!_dictByNamespace.ContainsKey(key))
            {
                _dictByNamespace[key] = new List<INamedTypeSymbol>();
            }

            _dictByNamespace[key].Add(symbol);
        }

        public bool CheckForTypeExists(
            string namespaceName,
            string typeName
            )
        {
            if (!_dictByNamespace.TryGetValue(
                namespaceName,
                out var typesInTargetNamespace
                ))
            {
                return false;
            }

            if (typesInTargetNamespace == null)
            {
                return false;
            }

            foreach(var titn in typesInTargetNamespace)
            {
                var nn = titn.ContainingNamespace.ToFullDisplayString();
                if(nn != namespaceName)
                {
                    continue;
                }

                var tn = titn.Name;
                if(tn != typeName)
                {
                    continue;
                }

                return true;
            }

            return false;
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

            await TaskScheduler.Default;

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

        public IReadOnlyDictionary<string, NamedTypeExtension> DictByFullName => _dictByFullName;

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

            await TaskScheduler.Default;

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

    public readonly struct NamedTypeExtension
    {
        public readonly INamedTypeSymbol Type;
        public readonly string TypeFullName;
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
