using Microsoft.CodeAnalysis.Text;
using System;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Edit
{
    /// <summary>
    /// A piece of the text of the file has to be replaced with another text: a fully qualified
    /// name which mentions the old namespace (<c>A.B.Class1</c>) is rewritten in place.
    ///
    /// The subject is a span of the text and not a syntax node: a file which several projects
    /// compile has a syntax tree per project, the references are found in all of them, and
    /// a name which is guarded by a conditional compilation symbol is a disabled text
    /// (i.e. a trivia and not a name) in a part of these trees. The text is the same for all
    /// of them, so a span is valid everywhere.
    /// </summary>
    [DebuggerDisplay("{FilePath}: {Span} -> {Text}")]
    public sealed class ReplaceTextEdit : FileEdit
    {
        /// <summary>
        /// The span of the text to be replaced.
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

        public ReplaceTextEdit(
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
            return obj is ReplaceTextEdit other
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
