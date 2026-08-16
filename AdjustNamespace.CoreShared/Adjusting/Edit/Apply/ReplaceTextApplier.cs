using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Edit.Apply
{
    /// <summary>
    /// Applies the <see cref="ReplaceTextEdit"/> of a file: all of them at once, as a single
    /// change of the text of the document.
    /// </summary>
    public static class ReplaceTextApplier
    {
        public static async Task ApplyAsync(
            Workspace workspace,
            string filePath,
            IReadOnlyList<ReplaceTextEdit> edits
            )
        {
            if (edits.Count == 0)
            {
                return;
            }

            await workspace.ApplyModifiedDocumentAsync(
                filePath,
                (document, syntaxRoot) =>
                {
                    var changes = BuildChanges(edits);
                    if (changes.Count == 0)
                    {
                        //nothing to do
                        return null;
                    }

                    var changedDocument = document.WithText(
                        syntaxRoot.GetText().WithChanges(changes)
                        );
                    return changedDocument;
                }
                );
        }

        /// <summary>
        /// The scheduled replacements as the changes of the text of the file.
        /// </summary>
        private static List<TextChange> BuildChanges(
            IReadOnlyList<ReplaceTextEdit> edits
            )
        {
            //`SourceText.WithChanges` does not accept the intersecting changes, while
            //a nested name may well be scheduled twice (`A.B.Outer.Inner` is a reference
            //to `Outer` and a reference to `Inner` at once). The longest one wins,
            //exactly as it does when the syntax nodes are replaced.
            var ordered = edits
                .OrderBy(e => e.Span.Start)
                .ThenByDescending(e => e.Span.Length)
                .ToList();

            var result = new List<TextChange>(ordered.Count);

            var lastEnd = -1;
            foreach (var edit in ordered)
            {
                if (edit.Span.Start < lastEnd)
                {
                    //inside of (or intersecting with) an already scheduled replacement
                    continue;
                }

                result.Add(
                    new TextChange(
                        edit.Span,
                        edit.Text
                        )
                    );

                lastEnd = edit.Span.End;
            }

            return result;
        }
    }
}
