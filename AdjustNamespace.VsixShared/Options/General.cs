using AdjustNamespace.TextReferences;
using Community.VisualStudio.Toolkit;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AdjustNamespace.Options
{
    /// <summary>
    /// Provider of the option pages of the extension
    /// (`Tools -> Options -> Adjust Namespaces`).
    /// </summary>
    internal partial class OptionsProvider
    {
        /// <summary>
        /// The `General` option page.
        /// </summary>
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General>
        {
        }
    }

    /// <summary>
    /// Options of the extension. They are stored per user (not per solution)
    /// and are not intended to be edited by hand, hence every property is not browsable.
    /// </summary>
    public class General : BaseOptionModel<General>
    {
        /// <summary>
        /// How many files have been adjusted so far. Used to decide when
        /// it is a good moment to ask the user for a rating.
        /// </summary>
        [Category("General")]
        [DisplayName("FilesAdjusted")]
        [Description("How many files were processed (adjusted namespaces).")]
        [DefaultValue(0)]
        [Browsable(false)]
        public int FilesAdjusted { get; set; } = 0;

        /// <summary>
        /// The user has already rated the extension, so we do not bother them anymore.
        /// </summary>
        [Category("General")]
        [DisplayName("StarsGiven")]
        [Description("Stars are given already, no need to make a noise.")]
        [DefaultValue(false)]
        [Browsable(false)]
        public bool StarsGiven { get; set; } = false;

        /// <summary>
        /// The last version of the extension the user has been informed about.
        /// If it differs from the installed one, the release notes gold bar is shown.
        /// </summary>
        [Category("Logic")]
        [DisplayName("Last Version")]
        [DefaultValue("0.0.0")]
        [Browsable(false)]
        public string LastVersion { get; set; } = "0.0.0";

        /// <summary>
        /// When the wizard shows the report of the run instead of closing right after it.
        /// </summary>
        [Category("Report")]
        [DisplayName("Show the report")]
        [Description("When the wizard shows what has been adjusted and what has not after the run: always, only if something was not adjusted or was left, or never (the window closes as soon as the run is over).")]
        [DefaultValue(ReportVisibility.Always)]
        public ReportVisibility ShowReport { get; set; } = ReportVisibility.Always;

        /// <summary>
        /// Count the compilation errors before and after the run.
        /// </summary>
        [Category("Report")]
        [DisplayName("Run the build check")]
        [Description("Count the compilation errors of the affected projects before the run and after it, and list the new ones in the report and in the Error List. It compiles the projects, so it takes time on a big solution.")]
        [DefaultValue(false)]
        public bool RunBuildCheck { get; set; } = false;

        /// <summary>
        /// The default aggressiveness of the search of the text references.
        /// </summary>
        [Category("Text references")]
        [DisplayName("Aggressiveness")]
        [Description("The names of the moved types which are written as a text (Type.GetType(\"A.B.C\"), a config file, a Razor directive) are not seen by Roslyn. Off: no search. ReportOnly: search and list, change nothing. Conservative: change the certain matches, offer the others. Balanced: change the certain and the likely matches, offer the possible ones. Aggressive: change the certain, the likely and the possible matches. A solution may override it in adjust_namespaces_settings.xml; the wizard shows it on the selection step.")]
        [DefaultValue(TextReferenceMode.Balanced)]
        public TextReferenceMode TextReferenceMode { get; set; } = TextReferenceMode.Balanced;

        /// <summary>
        /// Search the comments of C# for the text references.
        /// </summary>
        [Category("Text references")]
        [DisplayName("Search the comments")]
        [Description("Search the comments and the text of the documentation comments of C# files.")]
        [DefaultValue(false)]
        public bool ScanComments { get; set; } = false;

        /// <summary>
        /// Search the xml configs for the text references.
        /// </summary>
        [Category("Text references")]
        [DisplayName("Search the xml configs")]
        [Description("Search app.config, web.config, nlog.config and the other xml configs.")]
        [DefaultValue(true)]
        public bool ScanXmlConfigs { get; set; } = true;

        /// <summary>
        /// Search the json files for the text references.
        /// </summary>
        [Category("Text references")]
        [DisplayName("Search the json files")]
        [Description("Search appsettings*.json and the other json configs.")]
        [DefaultValue(true)]
        public bool ScanJson { get; set; } = true;

        /// <summary>
        /// Search the Razor files for the text references.
        /// </summary>
        [Category("Text references")]
        [DisplayName("Search the Razor files")]
        [Description("Search the directives (@using, @model, @inherits, @inject, ...) of .cshtml and .razor files.")]
        [DefaultValue(true)]
        public bool ScanRazor { get; set; } = true;

        /// <summary>
        /// Search the project files and the settings files for the text references.
        /// </summary>
        [Category("Text references")]
        [DisplayName("Search the project and settings files")]
        [Description("Search StartupObject and CustomToolNamespace of the project files and GeneratedClassNamespace of the .settings files.")]
        [DefaultValue(true)]
        public bool ScanProjectFiles { get; set; } = true;

        /// <summary>
        /// The options as the search of the text references takes them.
        /// </summary>
        public TextReferenceOptions ToTextReferenceOptions()
        {
            var kinds = TextReferenceKinds.None;

            if (ScanComments)
            {
                kinds |= TextReferenceKinds.Comments;
            }

            if (ScanXmlConfigs)
            {
                kinds |= TextReferenceKinds.XmlConfigs;
            }

            if (ScanJson)
            {
                kinds |= TextReferenceKinds.Json;
            }

            if (ScanRazor)
            {
                kinds |= TextReferenceKinds.Razor;
            }

            if (ScanProjectFiles)
            {
                kinds |= TextReferenceKinds.ProjectFiles;
            }

            return new TextReferenceOptions(TextReferenceMode, kinds);
        }
    }
}
