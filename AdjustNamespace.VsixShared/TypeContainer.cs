using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    /// <summary>
    /// Type (INamedTypeSymbol) container.
    /// </summary>
    public readonly struct TypeContainer
    {
        private readonly Dictionary<string, NamedTypeExtension> _dict = new ();

        public IReadOnlyDictionary<string, NamedTypeExtension> Dict => _dict;

        public TypeContainer(
            )
        {
            _dict = new Dictionary<string, NamedTypeExtension>();
        }

        /// <summary>
        /// Check if this type is in container.
        /// </summary>
        public bool ContainsType(string typeFullName)
        {
            return _dict.ContainsKey(typeFullName);
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
            _dict[typeFullName] = new NamedTypeExtension(symbol, typeFullName, containingNamespaceName);
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

            var result = new TypeContainer();
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
