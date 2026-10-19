using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.TextReferences;
using System.Collections.Generic;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Parameters of the performing wizard step.
    /// </summary>
    public readonly struct PerformingParameters
    {
        /// <summary>
        /// Full paths of the files to adjust (checked by the user on the previous step).
        /// </summary>
        public readonly List<string> SubjectFilePaths;

        /// <summary>
        /// User defined regex which additionally modifies the target namespace.
        /// </summary>
        public readonly NamespaceReplaceRegex ReplaceRegex;

        /// <summary>
        /// The files the scan has found impossible to adjust; the report says they were left.
        /// </summary>
        public readonly IReadOnlyList<AdjustBlock> Blocked;

        /// <summary>
        /// The search of the text references the user has reviewed: its approved matches are
        /// changed by the run. Empty if the search is off.
        /// </summary>
        public readonly TextReferenceScanResult TextReferences;

        public PerformingParameters(
            List<string> subjectFilePaths,
            NamespaceReplaceRegex replaceRegex,
            IReadOnlyList<AdjustBlock> blocked,
            TextReferenceScanResult textReferences
            )
        {
            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            if (blocked is null)
            {
                throw new ArgumentNullException(nameof(blocked));
            }

            if (textReferences is null)
            {
                throw new ArgumentNullException(nameof(textReferences));
            }

            SubjectFilePaths = subjectFilePaths;
            ReplaceRegex = replaceRegex;
            Blocked = blocked;
            TextReferences = textReferences;
        }
    }
}
