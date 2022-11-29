using AdjustNamespace.Adjusting.Fixer.Specific;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Adjusting.Fixer
{
    /// <summary>
    /// All fixer for specific file.
    /// </summary>
    public readonly struct FixerSet
    {
        private readonly VsServices _vss;
        private readonly bool _openFilesToEnableUndo;

        public readonly string FilePath;

        private readonly List<IFixer> _fixers;

        public FixerSet(
            VsServices vss,
            bool openFilesToEnableUndo,
            string filePath
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            _vss = vss;
            _openFilesToEnableUndo = openFilesToEnableUndo;
            FilePath = filePath;

            _fixers = new List<IFixer>
            {
                new QualifiedNameFixer(vss.Workspace, filePath),
                new AddUsingFixer(vss.Workspace, filePath),
                new NamespaceFixer(vss, filePath),
            };
        }

        public T Fixer<T>()
            where T : IFixer
        {
            var result = _fixers.FirstOrDefault(f => f is T);
            if (result == null)
            {
                throw new InvalidOperationException($"Unknown fixer {typeof(T).FullName}");
            }

            return (T)result;
        }

        internal async System.Threading.Tasks.Task FixAllAsync()
        {
            if (_openFilesToEnableUndo)
            {
                await _vss.OpenFileAsync(FilePath);
            }

            foreach (var fixer in _fixers)
            {
                await fixer.FixAsync();
            }
        }
    }
}
