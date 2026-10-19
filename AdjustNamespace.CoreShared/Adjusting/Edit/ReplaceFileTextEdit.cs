using Microsoft.CodeAnalysis.Text;
using System;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Edit
{
    /// <summary>
    /// A piece of the text of a file that is no C# document (a config, a Razor view, a project
    /// file) has to be replaced with another text.
    ///
    /// The file is changed as a text: it is never parsed and written back through a DOM, so the
    /// formatting, the encoding, the byte order mark and the line breaks stay as they are, the
    /// rule the xaml files follow as well. A C# file is a document of the Roslyn workspace and
    /// is changed with a <see cref="ReplaceTextEdit"/> instead.
    /// </summary>
    [DebuggerDisplay("{FilePath}: {Span} -> {Text}")]
    public sealed class ReplaceFileTextEdit : FileEdit
    {
        /// <summary>
        /// The span of the text to be replaced, in the text of the file as it is now.
        /// </summary>
        public TextSpan Span
        {
            get;
        }

        /// <summary>
        /// The text to be written instead of it.
        /// </summary>
        public string Text
        {
            get;
        }

        public ReplaceFileTextEdit(
            string filePath,
            TextSpan span,
            string text
            )
            : base(filePath)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Span = span;
            Text = text;
        }

        public override bool Equals(object? obj)
        {
            return obj is ReplaceFileTextEdit other
                && other.FilePath == FilePath
                && other.Span == Span
                && other.Text == Text
                ;
        }

        public override int GetHashCode()
        {
            return FilePath.GetHashCode() ^ Span.GetHashCode() ^ Text.GetHashCode();
        }

        public override string ToString()
        {
            return $"{FilePath}: [{Span.Start}..{Span.End}) -> {Text}";
        }
    }
}
