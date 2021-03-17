using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;

namespace AdjustNamespace.Adjusting
{
    public class NamespaceCenter
    {
        private readonly Dictionary<string, HashSet<string>> _allSolutionTypes;
        private readonly HashSet<string> _namespacesToRemove;

        public IReadOnlyCollection<string> NamespacesToRemove => _namespacesToRemove;

        public NamespaceCenter(
            IEnumerable<ITypeSymbol> allSolutionTypes
            )
        {
            if (allSolutionTypes is null)
            {
                throw new ArgumentNullException(nameof(allSolutionTypes));
            }

            _allSolutionTypes = new Dictionary<string, HashSet<string>>();
            foreach (var type in allSolutionTypes)
            {
                var key = type.ContainingNamespace.ToDisplayString();
                if (!_allSolutionTypes.ContainsKey(key))
                {
                    _allSolutionTypes[key] = new HashSet<string>();
                }
                _allSolutionTypes[key].Add(type.ToDisplayString());
            }

            _namespacesToRemove = new HashSet<string>();
        }

        public void TypeRemoved(ITypeSymbol type)
        {
            var cnn = type.ContainingNamespace.ToDisplayString();
            if (!_allSolutionTypes.ContainsKey(cnn))
            {
                return;
            }

            var tn = type.ToDisplayString();

            var set = _allSolutionTypes[cnn];
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
    }
}
