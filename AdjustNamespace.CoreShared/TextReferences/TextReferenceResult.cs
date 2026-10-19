using System;
using System.Diagnostics;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// What a run has done with a text reference: changed it or left it, and why it was left.
    /// </summary>
    [DebuggerDisplay("{(IsChanged ? \"changed\" : \"left\")}: {Match}")]
    public sealed class TextReferenceResult
    {
        /// <summary>
        /// The match.
        /// </summary>
        public TextReferenceMatch Match
        {
            get;
        }

        /// <summary>
        /// The run has changed the text.
        /// </summary>
        public bool IsChanged
        {
            get;
        }

        /// <summary>
        /// Why the text has been left: below the threshold of the mode, unchecked by the
        /// user, changed in the meantime. <c>null</c> for a changed text.
        /// </summary>
        public string? Reason
        {
            get;
        }

        public TextReferenceResult(
            TextReferenceMatch match,
            bool isChanged,
            string? reason
            )
        {
            if (match is null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            Match = match;
            IsChanged = isChanged;
            Reason = reason;
        }
    }
}
