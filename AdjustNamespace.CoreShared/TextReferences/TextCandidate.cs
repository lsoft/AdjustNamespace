using System.Diagnostics;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// A part of a file that may name a type: the content of a string literal, a comment, an
    /// attribute value, a Razor directive. A scanner finds them and knows nothing about the
    /// moved types; <see cref="TextReferenceAnalyzer"/> looks for the names in them and scores
    /// what it finds.
    /// </summary>
    [DebuggerDisplay("{Place} [{Start}, {Length}]")]
    internal readonly struct TextCandidate
    {
        /// <summary>
        /// Where the text is found: the base of the score.
        /// </summary>
        public TextReferencePlace Place
        {
            get;
        }

        /// <summary>
        /// Where the content starts in the text of the file (the quotes are not a part of it).
        /// </summary>
        public int Start
        {
            get;
        }

        /// <summary>
        /// The length of the content.
        /// </summary>
        public int Length
        {
            get;
        }

        /// <summary>
        /// The content is expected to be a namespace (a Razor <c>@using</c>,
        /// <c>GeneratedClassNamespace</c>), and not a type.
        /// </summary>
        public bool ExpectsNamespace
        {
            get;
        }

        /// <summary>
        /// The content is also searched for the names of the types (<c>@using static A.B.C</c>).
        /// </summary>
        public bool AlsoSearchTypes
        {
            get;
        }

        /// <summary>
        /// A note to the report that a match here deserves (a json <c>$type</c>), or <c>null</c>.
        /// </summary>
        public string? Note
        {
            get;
        }

        public TextCandidate(
            TextReferencePlace place,
            int start,
            int length,
            bool expectsNamespace = false,
            bool alsoSearchTypes = true,
            string? note = null
            )
        {
            Place = place;
            Start = start;
            Length = length;
            ExpectsNamespace = expectsNamespace;
            AlsoSearchTypes = alsoSearchTypes;
            Note = note;
        }
    }
}
