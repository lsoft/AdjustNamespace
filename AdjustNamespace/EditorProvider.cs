using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace AdjustNamespace
{
    public class EditorProvider
    {
        private readonly Workspace _workspace;

        private DocumentEditor? _editor = null;

        public EditorProvider(
            Workspace workspace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            _workspace = workspace;
        }

        public async Task<DocumentEditor?> GetDocumentEditorAsync(string filePath)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (_editor != null)
            {
                if (_editor.OriginalDocument.FilePath == filePath)
                {
                    return _editor;
                }

                await SaveAndClearAsync();
            }

            var document = _workspace.GetDocument(filePath);
            if (document == null)
            {
                return null;
            }

            _editor = await DocumentEditor.CreateAsync(document);
            return _editor;
        }


        public async Task<DocumentEditor?> GetDocumentEditorAsync(Document document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (_editor != null)
            {
                if (_editor.OriginalDocument.FilePath == document.FilePath)
                {
                    return _editor;
                }

                await SaveAndClearAsync();
            }

            _editor = await DocumentEditor.CreateAsync(document);
            return _editor;
        }


        public async Task SaveAndClearAsync()
        {
            if (_editor != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var changedDocument = _editor.GetChangedDocument();
                _workspace.TryApplyChanges(changedDocument.Project.Solution);

                await TaskScheduler.Default;
            }

            _editor = null;
        }

    }
}
