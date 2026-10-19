using Microsoft.CodeAnalysis.Text;
using System;
using System.Diagnostics;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// A name of a moved type that is written as a text, with the score of it.
    /// </summary>
    [DebuggerDisplay("{FilePath}:{Line} {MatchedText} -> {ReplacementText} [{Score}]")]
    public sealed class TextReferenceMatch
    {
        /// <summary>
        /// Number of the match in its scan (the order of the files and of the positions).
        /// </summary>
        public int Id
        {
            get;
            internal set;
        }

        /// <summary>
        /// Full path to the file.
        /// </summary>
        public string FilePath
        {
            get;
        }

        /// <summary>
        /// What kind of file it is.
        /// </summary>
        public TextReferenceFileKind FileKind
        {
            get;
        }

        /// <summary>
        /// Where in the file the name is written.
        /// </summary>
        public TextReferencePlace Place
        {
            get;
        }

        /// <summary>
        /// The span of the name in the text of the file as it is now.
        /// </summary>
        public TextSpan Span
        {
            get;
        }

        /// <summary>
        /// The line of the name, starting with 1.
        /// </summary>
        public int Line
        {
            get;
        }

        /// <summary>
        /// The column of the name in its line, starting with 0.
        /// </summary>
        public int Column
        {
            get;
        }

        /// <summary>
        /// The text of the line, without the line break.
        /// </summary>
        public string LineText
        {
            get;
        }

        /// <summary>
        /// The name as it is written: <c>A.B.C</c>.
        /// </summary>
        public string MatchedText
        {
            get;
        }

        /// <summary>
        /// What the name is going to be: <c>X.Y.C</c>.
        /// </summary>
        public string ReplacementText
        {
            get;
        }

        /// <summary>
        /// The moved type that is named, or <c>null</c> if the text names a namespace.
        /// </summary>
        public MovedType? Type
        {
            get;
        }

        /// <summary>
        /// The moved namespace that is named, or <c>null</c> if the text names a type.
        /// </summary>
        public MovedNamespace? Namespace
        {
            get;
        }

        /// <summary>
        /// The score and the signals it is made of.
        /// </summary>
        public ConfidenceScore Score
        {
            get;
        }

        /// <summary>
        /// A note to the report (a json <c>$type</c> may exist as serialized data elsewhere), or <c>null</c>.
        /// </summary>
        public string? Note
        {
            get;
        }

        /// <summary>
        /// The level of the score.
        /// </summary>
        public ConfidenceLevel Level => Score.Level;

        /// <summary>
        /// The match is going to be changed by the run. The scan presets it by the mode, the
        /// review step of the wizard lets the user change it.
        /// </summary>
        public bool IsApproved
        {
            get;
            set;
        }

        internal TextReferenceMatch(
            string filePath,
            TextReferenceFileKind fileKind,
            TextReferencePlace place,
            TextSpan span,
            int line,
            int column,
            string lineText,
            string matchedText,
            string replacementText,
            MovedType? type,
            MovedNamespace? @namespace,
            ConfidenceScore score,
            string? note
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            FileKind = fileKind;
            Place = place;
            Span = span;
            Line = line;
            Column = column;
            LineText = lineText;
            MatchedText = matchedText;
            ReplacementText = replacementText;
            Type = type;
            Namespace = @namespace;
            Score = score;
            Note = note;
        }
    }
}
