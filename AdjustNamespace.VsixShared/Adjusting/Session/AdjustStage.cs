namespace AdjustNamespace.Adjusting.Session
{
    /// <summary>
    /// The stages of an <see cref="AdjustSession"/>, in the order they are performed.
    /// </summary>
    public enum AdjustStage
    {
        /// <summary>
        /// The chosen files are adjusted one by one.
        /// </summary>
        Adjusting,

        /// <summary>
        /// The using clauses of the emptied namespaces are removed from the whole solution.
        /// </summary>
        Cleanup
    }
}
