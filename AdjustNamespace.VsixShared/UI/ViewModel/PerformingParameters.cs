using System.Collections.Generic;

namespace AdjustNamespace.UI.ViewModel
{
    public readonly struct PerformingParameters
    {
        public readonly List<string> SubjectFilePaths;
        public readonly NamespaceReplaceRegex ReplaceRegex;
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
