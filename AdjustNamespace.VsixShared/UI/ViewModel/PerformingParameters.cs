using System.Collections.Generic;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Parameters of the third (last) wizard step.
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
        /// Open the changed files in the editor (this allows the user to undo the changes).
        /// </summary>
        public readonly bool OpenFilesToEnableUndo;

        public PerformingParameters(
            List<string> subjectFilePaths,
            NamespaceReplaceRegex replaceRegex,
            bool openFilesToEnableUndo
            )
        {
            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            SubjectFilePaths = subjectFilePaths;
            ReplaceRegex = replaceRegex;
            OpenFilesToEnableUndo = openFilesToEnableUndo;
        }
    }
}
