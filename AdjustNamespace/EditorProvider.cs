using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

                var changedDocument = _editor.GetChangedDocument();
                _workspace.TryApplyChanges(changedDocument.Project.Solution);

                _editor = null;
            }

            var document = _workspace.GetDocument(filePath);
            if (document == null)
            {
                return null;
            }

            _editor = await DocumentEditor.CreateAsync(document);
            return _editor;
        }


        public void SaveAndClear()
        {
            if (_editor != null)
            {
                var changedDocument = _editor.GetChangedDocument();
                _workspace.TryApplyChanges(changedDocument.Project.Solution);
            }

            _editor = null;
        }

    }
}
