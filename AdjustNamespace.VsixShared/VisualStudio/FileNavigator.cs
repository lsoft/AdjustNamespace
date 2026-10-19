using Microsoft.VisualStudio.Text;
using System;
using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// Opens a file in the editor of Visual Studio at a line: what the report and the review
    /// of the text references do on a double click.
    /// </summary>
    public static class FileNavigator
    {
        /// <summary>
        /// Open the file and put the caret at the start of the line.
        /// </summary>
        /// <param name="filePath">Full path to the file.</param>
        /// <param name="line">The line, starting with 1; 0 opens the file only.</param>
        /// <param name="column">The column, starting with 0; clamped to the line.</param>
        public static async Task OpenAsync(
            string filePath,
            int line,
            int column = 0
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var documentView = await VS.Documents.OpenAsync(filePath);

            var textView = documentView?.TextView;
            if (textView is null || line <= 0)
            {
                return;
            }

            var snapshot = textView.TextSnapshot;
            if (snapshot.LineCount == 0)
            {
                return;
            }

            var snapshotLine = snapshot.GetLineFromLineNumber(Math.Min(line, snapshot.LineCount) - 1);
            var point = new SnapshotPoint(
                snapshot,
                snapshotLine.Start.Position + Math.Max(0, Math.Min(column, snapshotLine.Length))
                );

            textView.Caret.MoveTo(point);
            textView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(point, 0));
        }
    }
}
