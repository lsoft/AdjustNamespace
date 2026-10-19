namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// How sure the search is that a text really names a moved type. The levels are the ranges
    /// of the score (0..100): Certain 90..100, Likely 70..89, Possible 45..69, Unlikely 0..44.
    /// </summary>
    public enum ConfidenceLevel
    {
        /// <summary>
        /// 0..44. Hidden unless asked for.
        /// </summary>
        Unlikely = 0,

        /// <summary>
        /// 45..69.
        /// </summary>
        Possible = 1,

        /// <summary>
        /// 70..89.
        /// </summary>
        Likely = 2,

        /// <summary>
        /// 90..100.
        /// </summary>
        Certain = 3
    }
}
