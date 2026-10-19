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
        Cleanup,

        /// <summary>
        /// The files are searched for the text references to the moved types. The stage is
        /// there only when the search is asked for; it comes before the adjusting.
        /// </summary>
        TextReferences,

        /// <summary>
        /// The compilation errors of the solution are counted before the run and after it.
        /// The stage is there only when the build check is asked for.
        /// </summary>
        BuildCheck
    }
}
