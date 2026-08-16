using AdjustNamespace.VisualStudio;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdjustNamespace;

namespace AdjustNamespace.Adjusting.Edit.Apply
{
    /// <summary>
    /// Writes an <see cref="EditSet"/> into the solution.
    ///
    /// This is the only part of the adjusting which mutates anything: everything before it
    /// only decides what has to be changed.
    /// </summary>
    public sealed class EditApplier
    {
        private readonly Workspace _workspace;
        private readonly IDocumentOpener _documentOpener;
        private readonly bool _openFilesToEnableUndo;

        /// <param name="workspace">Roslyn workspace of the solution.</param>
        /// <param name="documentOpener">Opener of the changed files in the editor.</param>
        /// <param name="openFilesToEnableUndo">Open the changed files in the editor (this allows the user to undo the changes).</param>
        public EditApplier(
            Workspace workspace,
            IDocumentOpener documentOpener,
            bool openFilesToEnableUndo
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (documentOpener is null)
            {
                throw new ArgumentNullException(nameof(documentOpener));
            }

            _workspace = workspace;
            _documentOpener = documentOpener;
            _openFilesToEnableUndo = openFilesToEnableUndo;
        }

        /// <summary>
        /// Apply every edit of the set, file by file.
        /// </summary>
        public async Task ApplyAsync(EditSet edits)
        {
            if (edits is null)
            {
                throw new ArgumentNullException(nameof(edits));
            }

            foreach (var filePath in edits.FilePaths)
            {
                AdjustLog.WriteLine($"Fix references in {filePath}");

                await ApplyToFileAsync(filePath, edits.EditsOf(filePath));
            }
        }

        /// <summary>
        /// Apply the edits of a single file.
        ///
        /// The order of the kinds matters and is the reason they are not applied one by one:
        /// the fully qualified names are rewritten first (they are addressed by their spans
        /// in the text and any other edit invalidates these spans), then the <c>using</c>
        /// clauses are added and only then the namespace declarations are moved.
        /// </summary>
        private async Task ApplyToFileAsync(
            string filePath,
            IReadOnlyList<FileEdit> fileEdits
            )
        {
            if (_openFilesToEnableUndo)
            {
                await _documentOpener.OpenAsync(filePath);
            }

            await ReplaceTextApplier.ApplyAsync(
                _workspace,
                filePath,
                fileEdits.OfType<ReplaceTextEdit>().ToList()
                );

            await AddUsingApplier.ApplyAsync(
                _workspace,
                filePath,
                fileEdits.OfType<AddUsingEdit>().ToList()
                );

            foreach (var edit in fileEdits.OfType<MoveNamespaceEdit>())
            {
                await MoveNamespaceApplier.ApplyAsync(
                    _workspace,
                    filePath,
                    edit.Transition
                    );
            }
        }
    }
}
