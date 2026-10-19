using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.TextReferences;
using System.Collections.Generic;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Parameters of the review step, the one between the selection and the run.
    /// </summary>
    public readonly struct ReviewParameters
    {
        /// <summary>
        /// Full paths of the files chosen by the user on the very first step (the selection
        /// step is entered with them again when the user goes back).
        /// </summary>
        public readonly HashSet<string> FilePaths;

        /// <summary>
        /// Full paths of the files to adjust (checked by the user on the selection step).
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
        /// What the search of the text references has found (empty if the search is off).
        /// </summary>
        public readonly TextReferenceScanResult TextReferences;

        public ReviewParameters(
            HashSet<string> filePaths,
            List<string> subjectFilePaths,
            NamespaceReplaceRegex replaceRegex,
            IReadOnlyList<AdjustBlock> blocked,
            TextReferenceScanResult textReferences
            )
        {
            if (filePaths is null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }

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

            FilePaths = filePaths;
            SubjectFilePaths = subjectFilePaths;
            ReplaceRegex = replaceRegex;
            Blocked = blocked;
            TextReferences = textReferences;
        }
    }
}
