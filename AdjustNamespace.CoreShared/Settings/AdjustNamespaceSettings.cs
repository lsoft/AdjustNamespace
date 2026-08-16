using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdjustNamespace.Settings
{
    /// <summary>
    /// Settings of a solution. They are stored in the xml file
    /// (see <see cref="SettingsReader.SettingFileName"/>) in the solution folder,
    /// so they can be shared across the team through the source control.
    /// </summary>
    public class AdjustNamespaceSettings
    {
        /// <summary>
        /// Folders which must not take a part in the target namespace.
        /// A path is either a rooted one or a path relative to the solution folder.
        /// </summary>
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
