using AdjustNamespace.Adjusting.Fixer.Specific;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Adjusting.Fixer
{
    /// <summary>
    /// All fixer for specific file.
    /// The order of the fixers inside the set matters: the qualified names are fixed first
    /// (their spans become invalid after any other edit), then the using clauses are added
    /// and only then the namespace clauses are changed.
    /// </summary>
    public readonly struct FixerSet
    {
        private readonly VsServices _vss;
        private readonly bool _openFilesToEnableUndo;

        /// <summary>
        /// Full path to the file these fixers work with.
        /// </summary>
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

        /// <summary>
        /// Get the fixer of the requested type.
        /// </summary>
        /// <exception cref="InvalidOperationException">There is no fixer of such type in this set.</exception>
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

        /// <summary>
        /// Apply all the fixers of this file (in the order they have been created).
        /// </summary>
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
