namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// How aggressive the search of the text references to the moved types is: what is
    /// scanned, what is changed on its own and what is only offered.
    ///
    /// A "text reference" is a name of a moved type that Roslyn does not see because it is
    /// written as a text: a string passed to <c>Type.GetType</c>, a comment, the <c>type=</c>
    /// attribute of a config file, a Razor directive. Every match has a confidence level
    /// (<see cref="ConfidenceLevel"/>) and the mode decides which levels are changed.
    /// </summary>
    public enum TextReferenceMode
    {
        /// <summary>
        /// No search at all.
        /// </summary>
        Off,

        /// <summary>
        /// Search, change nothing, list everything from the level Possible.
        /// </summary>
        ReportOnly,

        /// <summary>
        /// Change the Certain matches, offer the Likely and the Possible ones.
        /// </summary>
        Conservative,

        /// <summary>
        /// Change the Certain and the Likely matches, offer the Possible ones. The default.
        /// </summary>
        Balanced,

        /// <summary>
        /// Change the Certain, the Likely and the Possible matches.
        /// </summary>
        Aggressive
    }
}
