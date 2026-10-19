using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Namespace;
using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Finds out which types a session is going to move and where to, out of the plans of the
    /// files: the same rules <c>CsAdjuster</c> follows when it moves them, applied before
    /// anything has been changed.
    /// </summary>
    public static class MovedTypeCollector
    {
        /// <summary>
        /// The types (and the namespaces) the given plans move.
        /// </summary>
        /// <param name="workspace">The Roslyn workspace of the solution.</param>
        /// <param name="plans">The plans of the files, in the order the session processes them.</param>
        /// <param name="cancellationToken">Cancellation of the session.</param>
        public static async Task<MovedTypeSet> CollectAsync(
            Workspace workspace,
            IReadOnlyList<AdjustPlanItem> plans,
            CancellationToken cancellationToken = default
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (plans is null)
            {
                throw new ArgumentNullException(nameof(plans));
            }

            var types = new List<MovedType>();
            var seen = new HashSet<string>();
            var lookalikeNamespaces = new Dictionary<string, bool>();

            foreach (var plan in plans)
            {
                if (plan.IsXaml)
                {
                    //the class of a xaml is named by the xaml, not by a text
                    continue;
                }

                foreach (var document in workspace.GetDocuments(plan.FilePath))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var root = await document.GetSyntaxRootAsync(cancellationToken);
                    var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                    var compilation = await document.Project.GetCompilationAsync(cancellationToken);
                    if (root == null || semanticModel == null || compilation == null)
                    {
                        continue;
                    }

                    var declarations = root.DescendantNodes()
                        .Where(n => n is TypeDeclarationSyntax || n is EnumDeclarationSyntax || n is DelegateDeclarationSyntax);

                    foreach (var declaration in declarations)
                    {
                        var symbol = semanticModel.GetDeclaredSymbol(declaration) as INamedTypeSymbol;
                        if (symbol == null || symbol.ContainingType != null)
                        {
                            continue;
                        }

                        var oldNamespace = symbol.ContainingNamespace.ToDisplayString();
                        if (oldNamespace == plan.TargetNamespace || NamespaceHelper.IsSpecialNamespace(oldNamespace))
                        {
                            continue;
                        }

                        //the transition of the very declaration the type is written in,
                        //exactly as CsAdjuster does it
                        var transition = NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf(
                            declaration,
                            plan.TargetNamespace
                            );
                        if (!transition.HasValue || oldNamespace == transition.Value.ModifiedName)
                        {
                            continue;
                        }

                        if (!seen.Add(symbol.ToDisplayString()))
                        {
                            continue;
                        }

                        types.Add(
                            new MovedType(
                                oldNamespace,
                                transition.Value.ModifiedName,
                                symbol.Name,
                                symbol.Arity,
                                symbol.ContainingAssembly?.Name,
                                NestedNamesOf(symbol),
                                HasLookalikeElsewhere(compilation, symbol),
                                symbol
                                )
                            );

                        if (!lookalikeNamespaces.ContainsKey(oldNamespace))
                        {
                            lookalikeNamespaces[oldNamespace] = HasLookalikeNamespace(compilation, oldNamespace);
                        }
                    }
                }
            }

            var namespaces = await BuildNamespacesAsync(workspace, types, lookalikeNamespaces);

            return new MovedTypeSet(types, namespaces);
        }

        /// <summary>
        /// Replay the moves on the namespace state of the solution, the way the session does it,
        /// to learn which of the namespaces are gone after the run.
        /// </summary>
        private static async Task<List<MovedNamespace>> BuildNamespacesAsync(
            Workspace workspace,
            List<MovedType> types,
            Dictionary<string, bool> lookalikeNamespaces
            )
        {
            var result = new List<MovedNamespace>();
            if (types.Count == 0)
            {
                return result;
            }

            var center = await NamespaceCenter.CreateForAsync(workspace);

            foreach (var type in types)
            {
                if (type.Symbol == null)
                {
                    continue;
                }

                center.TypeRemoved(type.Symbol);
                center.TypeAdded(type.Symbol, type.NewNamespace);
            }

            foreach (var group in types.GroupBy(t => t.OldNamespace))
            {
                result.Add(
                    new MovedNamespace(
                        group.Key,
                        group.Select(t => t.NewNamespace).Distinct().ToList(),
                        center.IsRemoved(group.Key),
                        lookalikeNamespaces.TryGetValue(group.Key, out var lookalike) && lookalike
                        )
                    );
            }

            return result;
        }

        /// <summary>
        /// The names of the types nested into the given one, in the metadata form.
        /// </summary>
        private static List<string> NestedNamesOf(INamedTypeSymbol type)
        {
            var result = new List<string>();

            foreach (var nested in type.GetTypeMembers())
            {
                result.Add(nested.Name);

                foreach (var deeper in NestedNamesOf(nested))
                {
                    result.Add(nested.Name + "+" + deeper);
                }
            }

            return result;
        }

        /// <summary>
        /// A type of the same full name is declared in another assembly the project sees.
        /// </summary>
        private static bool HasLookalikeElsewhere(Compilation compilation, INamedTypeSymbol type)
        {
            var metadataName = type.ContainingNamespace.ToDisplayString() + "." + type.MetadataName;

            return compilation.GetTypesByMetadataName(metadataName)
                .Any(t => !SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, type.ContainingAssembly));
        }

        /// <summary>
        /// A referenced assembly declares a namespace of that name as well.
        /// </summary>
        private static bool HasLookalikeNamespace(Compilation compilation, string namespaceName)
        {
            var @namespace = compilation.TryFindNamespace(namespaceName);

            return @namespace != null
                && @namespace.ConstituentNamespaces.Any(c => c.Locations.Any(l => l.IsInMetadata));
        }
    }
}
