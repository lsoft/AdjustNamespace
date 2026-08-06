using AdjustNamespace.VisualStudio;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace AdjustNamespace.Xaml.BodyProvider
{
    /// <summary>
    /// Body provider which works with the xaml file through the Visual Studio editor.
    /// The file is opened in the editor, so all the changes made this way are undoable
    /// by the user (Ctrl+Z), but a lot of opened documents slows Visual Studio down.
    /// The main thread is required for every method of this class.
    /// </summary>
    public sealed class OpenedXamlBodyProvider : IXamlBodyProvider
    {
        /// <summary>
        /// The opened document. <c>null</c> until <see cref="OpenAsync"/> has been called.
        /// </summary>
        private DocumentView? _dw;

        /// <inheritdoc/>
        public string XamlFilePath
        {
            get;
        }


        public OpenedXamlBodyProvider(
            string xamlFilePath
            )
        {
            if (xamlFilePath is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePath));
            }

            XamlFilePath = xamlFilePath;
        }

        /// <summary>
        /// Open the xaml file in the Visual Studio editor.
        /// Must be called before <see cref="ReadText"/> / <see cref="UpdateText"/>.
        /// </summary>
        public async Task OpenAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            //var (r, subjectProject, subjectProjectItem) = await SolutionHelper.TryGetProjectItemAsync(XamlFilePath);

            //if (!r)
            //{
            //    throw new InvalidOperationException($"File {XamlFilePath} not found!");
            //}

            _dw = await VS.Documents.OpenViaProjectAsync(XamlFilePath);
        }

        /// <inheritdoc/>
        public string ReadText()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (_dw == null)
            {
                throw new InvalidOperationException("Please open document before retrieving its text");
            }

            var result = _dw!.TextView!.TextSnapshot.GetText();
            return result;
        }

        /// <inheritdoc/>
        public void UpdateText(string text)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (_dw == null)
            {
                throw new InvalidOperationException("Please open document before saving its text");
            }

            var edit = _dw!.TextView!.TextBuffer.CreateEdit();
            edit.Delete(0, edit.Snapshot.GetText().Length);
            edit.Insert(0, text);
            edit.Apply();
        }
    }
}
