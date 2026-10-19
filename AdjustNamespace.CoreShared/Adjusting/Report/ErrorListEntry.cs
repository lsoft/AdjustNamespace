using System;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// A thing the run has not fixed, as a warning of the Error List of Visual Studio: it stays
    /// visible after the window of the wizard is closed.
    /// </summary>
    [DebuggerDisplay("{Text}")]
    public sealed class ErrorListEntry
    {
        /// <summary>
        /// The message.
        /// </summary>
        public string Text
        {
            get;
        }

        /// <summary>
        /// Full path to the file, or <c>null</c>.
        /// </summary>
        public string? FilePath
        {
            get;
        }

        /// <summary>
        /// The line, starting with 1; 0 if there is none.
        /// </summary>
        public int Line
        {
            get;
        }

        /// <summary>
        /// The column, starting with 0.
        /// </summary>
        public int Column
        {
            get;
        }

        public ErrorListEntry(
            string text,
            string? filePath,
            int line,
            int column
            )
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Text = text;
            FilePath = filePath;
            Line = line;
            Column = column;
        }
    }
}
