namespace AdjustNamespace.Adjusting.Session
{
    /// <summary>
    /// How an <see cref="AdjustSession"/> has ended.
    /// </summary>
    public enum AdjustSessionOutcome
    {
        /// <summary>
        /// Every chosen file has been processed and the cleanup has been performed.
        /// </summary>
        Completed,

        /// <summary>
        /// The user has cancelled the session. The changes which have been applied before
        /// that are not reverted, see <see cref="AdjustSession.RunAsync"/>.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The processing of a file has failed unexpectedly and the session has stopped there.
        /// The report names the file and carries the message; the files which were not reached
        /// are not processed.
        /// </summary>
        Failed
    }
}
