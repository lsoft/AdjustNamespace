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

        public T Fixer<T>(string filePath)
            where T : IFixer
        {
            if (!_dict.TryGetValue(filePath, out var fixerSet))
            {
                fixerSet = AddFixersFor(filePath);
            }

            return fixerSet.Fixer<T>();
        }

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
