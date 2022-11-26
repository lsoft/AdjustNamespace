using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdjustNamespace.Settings
{
    public class AdjustNamespaceSettings
    {
        public List<string> SkippedFolderSuffixes
        {
            get;
            set;
        } = null!;


        public AdjustNamespaceSettings()
        {
            SkippedFolderSuffixes = new List<string>();
        }
    }
}
