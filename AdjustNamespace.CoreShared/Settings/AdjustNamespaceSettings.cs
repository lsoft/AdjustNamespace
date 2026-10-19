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

        /// <summary>
        /// The aggressiveness of the search of the text references to the moved types for this
        /// solution: <c>Off</c>, <c>ReportOnly</c>, <c>Conservative</c>, <c>Balanced</c> or
        /// <c>Aggressive</c>. <c>null</c> leaves the choice to the user options.
        /// </summary>
        public string? TextReferenceMode
        {
            get;
            set;
        }

        /// <summary>
        /// Search the comments for the text references; <c>null</c> leaves the choice to the user options.
        /// </summary>
        public bool? ScanComments
        {
            get;
            set;
        }

        /// <summary>
        /// Search the xml configs for the text references; <c>null</c> leaves the choice to the user options.
        /// </summary>
        public bool? ScanXmlConfigs
        {
            get;
            set;
        }

        /// <summary>
        /// Search the json files for the text references; <c>null</c> leaves the choice to the user options.
        /// </summary>
        public bool? ScanJson
        {
            get;
            set;
        }

        /// <summary>
        /// Search the Razor files for the text references; <c>null</c> leaves the choice to the user options.
        /// </summary>
        public bool? ScanRazor
        {
            get;
            set;
        }

        /// <summary>
        /// Search the project files and the settings files for the text references; <c>null</c>
        /// leaves the choice to the user options.
        /// </summary>
        public bool? ScanProjectFiles
        {
            get;
            set;
        }

        /// <summary>
        /// The xml and json files besides the usual configs the search looks into. A name
        /// (<c>Services.xml</c>), a path relative to the solution folder (<c>Config\*.json</c>)
        /// or a pattern with <c>*</c> and <c>?</c>.
        /// </summary>
        public List<string> TextReferenceFiles
        {
            get;
            set;
        } = null!;


        public AdjustNamespaceSettings()
        {
            SkippedFolderSuffixes = new List<string>();
            TextReferenceFiles = new List<string>();
        }
    }
}
