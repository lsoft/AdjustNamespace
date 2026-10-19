namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The scale of the confidence: which score is which <see cref="ConfidenceLevel"/>.
    /// </summary>
    public static class Confidence
    {
        /// <summary>
        /// The lowest score of the level Certain.
        /// </summary>
        public const int CertainFrom = 90;

        /// <summary>
        /// The lowest score of the level Likely.
        /// </summary>
        public const int LikelyFrom = 70;

        /// <summary>
        /// The lowest score of the level Possible.
        /// </summary>
        public const int PossibleFrom = 45;

        /// <summary>
        /// The highest score a text can get: 100.
        /// </summary>
        public const int Max = 100;

        /// <summary>
        /// The highest score a match of a namespace (and not of a type) that is emptied can get:
        /// it is never "certain".
        /// </summary>
        public const int EmptiedNamespaceCap = 89;

        /// <summary>
        /// The level of the score.
        /// </summary>
        public static ConfidenceLevel LevelOf(int score)
        {
            if (score >= CertainFrom)
            {
                return ConfidenceLevel.Certain;
            }

            if (score >= LikelyFrom)
            {
                return ConfidenceLevel.Likely;
            }

            if (score >= PossibleFrom)
            {
                return ConfidenceLevel.Possible;
            }

            return ConfidenceLevel.Unlikely;
        }

        /// <summary>
        /// The score put into the range 0..100.
        /// </summary>
        public static int Clamp(int score)
        {
            if (score < 0)
            {
                return 0;
            }

            return score > Max ? Max : score;
        }
    }
}
