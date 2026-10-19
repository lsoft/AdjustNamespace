namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// What has happened with a file the user wanted to adjust.
    /// </summary>
    public enum AdjustFileStatus
    {
        /// <summary>
        /// The namespaces of the file have been moved.
        /// </summary>
        Adjusted,

        /// <summary>
        /// There was nothing to move any more (the file is in its namespace already).
        /// </summary>
        Skipped,

        /// <summary>
        /// The file cannot be adjusted safely, see <see cref="AdjustFileOutcome.Block"/>.
        /// </summary>
        Blocked,

        /// <summary>
        /// The session has been cancelled (or has failed) before this file was reached.
        /// </summary>
        NotProcessed,

        /// <summary>
        /// The processing of the file has failed, see <see cref="AdjustFileOutcome.Message"/>.
        /// </summary>
        Failed
    }
}
