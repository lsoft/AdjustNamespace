using AdjustNamespace.Adjusting.Fixer;
using AdjustNamespace.Adjusting.Fixer.Specific;
using AdjustNamespace.Helper;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;

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

        internal async Task FixAllAsync()
        {
            if (_openFilesToEnableUndo)
            {
                //await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                //VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, FilePath, Guid.Empty, out _, out _, out IVsWindowFrame? frame);
                //IVsTextView? nativeView = VsShellUtilities.GetTextView(frame);

                //await Task.Delay(2000);
                //var dh = new VisualStudioDocumentHelper(FilePath);
                //dh.OpenAndNavigate(0, 0, 0, 0);
                _vss.OpenFile(FilePath);
            }

            foreach (var fixer in _fixers)
            {
                await fixer.FixAsync();
            }
        }
    }
}
