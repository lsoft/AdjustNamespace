using System.ComponentModel;

namespace AdjustNamespace.Options
{
    internal class GeneralOptions : BaseOptionModel<GeneralOptions>
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
