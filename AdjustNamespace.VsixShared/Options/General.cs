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


    }
}
