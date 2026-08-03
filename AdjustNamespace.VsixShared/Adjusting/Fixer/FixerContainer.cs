using AdjustNamespace.Adjusting.Fixer;
using AdjustNamespace.Adjusting;
using Microsoft.VisualStudio.LanguageServices;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.CodeCleanUp;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Fixer
{
    /// <summary>
    /// Container for all fixers we produce.
    /// The fixers are grouped by the file they are applied to (see <see cref="FixerSet"/>).
    /// </summary>
    public class FixerContainer
    {
        private readonly VsServices _vss;
        private readonly bool _openFilesToEnableUndo;

        private readonly Dictionary<string, FixerSet> _dict = new();

        public FixerContainer(
            VsServices vss,
            bool openFilesToEnableUndo
            )
        {
            _vss = vss;
            _openFilesToEnableUndo = openFilesToEnableUndo;
        }

        /// <summary>
        /// Get the fixer of the requested type for the requested file.
        /// The fixers for a file are created on the first demand.
        /// </summary>
        public T Fixer<T>(string filePath)
            where T : IFixer
        {
            if (!_dict.TryGetValue(filePath, out var fixerSet))
            {
                fixerSet = AddFixersFor(filePath);
            }

            return fixerSet.Fixer<T>();
        }

        /// <summary>
        /// Apply the fixers of every touched file.
        /// </summary>
        public async Task FixAllAsync()
        {
            foreach (var pair in _dict)
            {
                var targetFilePath = pair.Key;

                Debug.WriteLine($"Fix references in {targetFilePath}");

                await pair.Value.FixAllAsync();
            }
        }

        private FixerSet AddFixersFor(string filePath)
        {
            var fs = new FixerSet(_vss, _openFilesToEnableUndo, filePath);
            _dict[filePath] = fs;
            return fs;
        }

    }
}
