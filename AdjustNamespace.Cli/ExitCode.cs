namespace AdjustNamespace.Cli
{
    /// <summary>
    /// What the utility returns to the shell.
    /// </summary>
    public enum ExitCode
    {
        /// <summary>
        /// The work is done, or there was nothing to do at all.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The utility was unable to do its job.
        /// </summary>
        Error = 1,

        /// <summary>
        /// <c>--check</c> has found the files which have to be adjusted.
        /// </summary>
        AdjustmentRequired = 2
    }
}
