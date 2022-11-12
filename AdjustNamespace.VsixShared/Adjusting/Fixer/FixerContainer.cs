using AdjustNamespace.Adjusting.Fixer;
using AdjustNamespace.Adjusting;
using Microsoft.VisualStudio.LanguageServices;
using System;
using System.Collections.Generic;
using System.Text;

namespace AdjustNamespace.Adjusting.Fixer
{
    /// <summary>
    /// Container for all fixers we produce.
    /// </summary>
    public class FixerContainer
    {
        private readonly Dictionary<string, FixerSet> _dict = new();
        private readonly VisualStudioWorkspace _workspace;

        public IReadOnlyDictionary<string, FixerSet> Dict => _dict;

        public FixerContainer(
            VisualStudioWorkspace workspace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            _workspace = workspace;
        }

        public void TryAddFixersFor(string filePath)
        {
            if (!_dict.ContainsKey(filePath))
            {
                _dict[filePath] = new FixerSet(_workspace, filePath);
            }
        }

        public T Fixer<T>(string filePath)
            where T : IFixer
        {
            if (!_dict.TryGetValue(filePath, out var fixerSet))
            {
                throw new InvalidOperationException($"No fixers found for {filePath}");
            }

            return fixerSet.Fixer<T>();
        }
    }
}
