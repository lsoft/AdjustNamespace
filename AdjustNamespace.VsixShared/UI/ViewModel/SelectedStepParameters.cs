using AdjustNamespace.UI.ViewModel;
using AdjustNamespace;
using System.Collections.Generic;

namespace AdjustNamespace.UI.ViewModel
{
    public readonly struct SelectedStepParameters
    {
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
