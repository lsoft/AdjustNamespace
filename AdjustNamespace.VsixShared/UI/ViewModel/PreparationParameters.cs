using System.Collections.Generic;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Parameters of the first wizard step.
    /// </summary>
    public readonly struct PreparationParameters
    {
        /// <summary>
        /// Full paths of the files chosen by the user.
        /// </summary>
        public readonly HashSet<string> FilePaths;

        public PreparationParameters(
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
