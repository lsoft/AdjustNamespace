using AdjustNamespace.Xaml.BodyProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Edit.Apply
{
    /// <summary>
    /// Applies the <see cref="ReplaceFileTextEdit"/> of a file: all of them at once, as a
    /// single change of the text of the file.
    ///
    /// The file is read and written through the body provider the host has chosen, which is
    /// what makes the change undoable in Visual Studio (an invisible text buffer that takes
    /// part in the global linked undo) and a plain file write elsewhere. The text is changed
    /// where the spans say and nowhere else.
    /// </summary>
    public static class ReplaceFileTextApplier
    {
        public static async Task ApplyAsync(
            IXamlBodyProviderFactory bodyProviders,
            string filePath,
            IReadOnlyList<ReplaceFileTextEdit> edits
            )
        {
            if (edits.Count == 0)
            {
                return;
            }

            var provider = await bodyProviders.CreateForWriteAsync(filePath);

            try
            {
                var original = provider.ReadText();
                var changed = Apply(original, edits);

                if (!string.Equals(original, changed, StringComparison.Ordinal))
                {
                    provider.UpdateText(changed);
                }
            }
            finally
            {
                (provider as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// The text with the replacements made. The spans are those of the original text, so
        /// the replacements are made from the end; the ones inside of (or intersecting with)
        /// an already made one are dropped.
        /// </summary>
        public static string Apply(
            string original,
            IReadOnlyList<ReplaceFileTextEdit> edits
            )
        {
            var ordered = edits
                .OrderBy(e => e.Span.Start)
                .ThenByDescending(e => e.Span.Length)
                .ToList();

            var accepted = new List<ReplaceFileTextEdit>();
            var lastEnd = -1;

            foreach (var edit in ordered)
            {
                if (edit.Span.Start < lastEnd || edit.Span.End > original.Length)
                {
                    continue;
                }

                accepted.Add(edit);
                lastEnd = edit.Span.End;
            }

            var result = new StringBuilder(original);

            for (var i = accepted.Count - 1; i >= 0; i--)
            {
                var edit = accepted[i];

                result.Remove(edit.Span.Start, edit.Span.Length);
                result.Insert(edit.Span.Start, edit.Text);
            }

            return result.ToString();
        }
    }
}
