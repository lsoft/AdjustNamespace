using Community.VisualStudio.Toolkit;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AdjustNamespace.Options
{
    internal partial class OptionsProvider
    {
        // Register the options with this attribute on your package class:
        // [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "AdjustNamespace2022", "General", 0, 0, true, SupportsProfiles = true)]
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General>
        {
        }
    }

    public class General : BaseOptionModel<General>
    {
        [Category("General")]
        [DisplayName("FilesAdjusted")]
        [Description("How many files were processed (adjusted namespaces).")]
        [DefaultValue(0)]
        [Browsable(false)]
        public int FilesAdjusted { get; set; } = 0;

        [Category("General")]
        [DisplayName("StarsGiven")]
        [Description("Stars are given already, no need to make noise.")]
        [DefaultValue(false)]
        [Browsable(false)]
        public bool StarsGiven { get; set; } = false;
    }
}
