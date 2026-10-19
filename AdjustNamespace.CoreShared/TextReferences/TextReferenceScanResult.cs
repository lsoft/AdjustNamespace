using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// What the search of the text references has found: every match with its score, and the
    /// options that decide which of them are changed, which are only offered and which are hidden.
    ///
    /// The approval of a match (<see cref="TextReferenceMatch.IsApproved"/>) is preset by the
    /// mode and is what the review step of the wizard lets the user change; the session changes
    /// the approved matches and reports the rest.
    /// </summary>
    public sealed class TextReferenceScanResult
    {
        /// <summary>
        /// Every match, by file and by position.
        /// </summary>
        public IReadOnlyList<TextReferenceMatch> Matches
        {
            get;
        }

        /// <summary>
        /// The options the search has been performed with.
        /// </summary>
        public TextReferenceOptions Options
        {
            get;
        }

        /// <summary>
        /// The matches the user is shown: changed by the run or offered, from the level Possible.
        /// </summary>
        public IEnumerable<TextReferenceMatch> Shown => Matches.Where(m => Options.IsShown(m.Level));

        /// <summary>
        /// The matches the run is going to change.
        /// </summary>
        public IEnumerable<TextReferenceMatch> Approved => Matches.Where(m => m.IsApproved);

        /// <summary>
        /// How many matches are hidden because they are unlikely.
        /// </summary>
        public int HiddenCount => Matches.Count(m => !Options.IsShown(m.Level) && !m.IsApproved);

        /// <summary>
        /// There is something to show in the review step.
        /// </summary>
        public bool HasAnythingToShow => Shown.Any();

        /// <summary>
        /// The search has been performed at all (the mode is not <c>Off</c>).
        /// </summary>
        public bool WasPerformed => Options.IsScanEnabled;

        public TextReferenceScanResult(
            IReadOnlyList<TextReferenceMatch> matches,
            TextReferenceOptions options
            )
        {
            if (matches is null)
            {
                throw new ArgumentNullException(nameof(matches));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Matches = matches;
            Options = options;
        }

        /// <summary>
        /// A search that has been given up: the mode is <c>Off</c>, or nothing is moved.
        /// </summary>
        public static TextReferenceScanResult Empty(TextReferenceOptions options)
        {
            return new TextReferenceScanResult(Array.Empty<TextReferenceMatch>(), options);
        }

        /// <summary>
        /// Approve what the mode of the options changes on its own and nothing else.
        /// </summary>
        public void ApproveByMode()
        {
            foreach (var match in Matches)
            {
                match.IsApproved = Options.IsApplied(match.Level);
            }
        }
    }
}
