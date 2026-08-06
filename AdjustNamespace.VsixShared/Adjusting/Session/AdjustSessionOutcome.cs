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
        Cancelled
    }
}
