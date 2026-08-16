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
    /// only decides what has to be changed. Inside Visual Studio the workspace applies the
    /// edits through invisible text buffers when a global linked undo transaction is open,
    /// so the user can undo the whole run with one Ctrl+Z without opening editor tabs.
    /// </summary>
    public sealed class EditApplier
    {
        private readonly Workspace _workspace;

        /// <param name="workspace">Roslyn workspace of the solution.</param>
        public EditApplier(
            Workspace workspace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            _workspace = workspace;
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
