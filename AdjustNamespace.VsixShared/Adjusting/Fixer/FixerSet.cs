using AdjustNamespace.Adjusting.Fixer;
using Microsoft.VisualStudio.LanguageServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Fixer
{
    /// <summary>
    /// All fixer for specific file.
    /// </summary>
    public readonly struct FixerSet
    {
        public readonly string FilePath;

        private readonly List<IFixer> _fixers;

        public FixerSet(
            VisualStudioWorkspace workspace,
            string filePath
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;

            _fixers = new List<IFixer>
            {
                new QualifiedNameFixer(workspace, filePath),
                new AddUsingFixer(workspace, filePath),
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

        internal async Task FixAllAsync()
        {
            foreach (var fixer in _fixers)
            {
                await fixer.FixAsync();
            }
        }
    }
}
