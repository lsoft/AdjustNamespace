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

        public SelectedStepParameters(
            HashSet<string> filePaths
            )
        {
            if (filePaths is null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }

            FilePaths = filePaths;
        }

    }
}
