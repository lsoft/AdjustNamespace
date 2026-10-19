namespace AdjustNamespace.Options
{
    /// <summary>
    /// When the wizard shows the report of the run instead of closing right after it.
    /// </summary>
    public enum ReportVisibility
    {
        /// <summary>
        /// After every run.
        /// </summary>
        Always,

        /// <summary>
        /// Only if something was not adjusted or was left: a blocked or failed file, a text
        /// reference which was not changed, a new compilation error.
        /// </summary>
        WhenSomethingIsLeft,

        /// <summary>
        /// Never: the window closes as soon as the run is over.
        /// </summary>
        Never
    }
}
