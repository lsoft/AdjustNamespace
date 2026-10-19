using AdjustNamespace.TextReferences;
using AdjustNamespace.UI.ViewModel;
using AdjustNamespace;
using System.Collections.Generic;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Parameters of the second wizard step.
    /// </summary>
    public readonly struct SelectedStepParameters
    {
        /// <summary>
        /// Full paths of the files chosen by the user.
        /// </summary>
        public readonly HashSet<string> FilePaths;

        /// <summary>
        /// The options of the search of the text references the user has chosen on this step
        /// before going forward and back again; <c>null</c> on the first visit (the options
        /// of the user and of the solution are taken then).
        /// </summary>
        public readonly TextReferenceOptions? TextReferenceOptions;

        public SelectedStepParameters(
            HashSet<string> filePaths,
            TextReferenceOptions? textReferenceOptions = null
            )
        {
            if (filePaths is null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }

            FilePaths = filePaths;
            TextReferenceOptions = textReferenceOptions;
        }

    }
}
