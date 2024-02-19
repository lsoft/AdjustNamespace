using AdjustNamespace.UI.ViewModel;
using AdjustNamespace;
using System.Collections.Generic;

namespace AdjustNamespace.UI.ViewModel
{
    public readonly struct SelectedStepParameters
    {
        public readonly List<FileEx> FileExs;
        public readonly NamespaceReplaceRegex ReplaceRegex;

        public SelectedStepParameters(List<FileEx> fileExs, NamespaceReplaceRegex replaceRegex)
        {
            FileExs = fileExs;
            ReplaceRegex = replaceRegex;
        }
    }
}
