using System;
using System.Diagnostics;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// One reason of the score of a match: the place the text is found in, or a modifier
    /// (an assembly name is next to it, it reads like a file name, ...) with its weight.
    ///
    /// The score is the sum of the signals, so "why this score" is data: the review step of
    /// the wizard shows it as a tooltip and the report prints it.
    /// </summary>
    [DebuggerDisplay("{Weight:+0;-0} {Name}")]
    public readonly struct ConfidenceSignal
    {
        /// <summary>
        /// The words the signal is shown as.
        /// </summary>
        public string Name
        {
            get;
        }

        /// <summary>
        /// How many points the signal adds to the score (or takes away, if it is negative).
        /// </summary>
        public int Weight
        {
            get;
        }

        public ConfidenceSignal(
            string name,
            int weight
            )
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            Weight = weight;
        }

        public override string ToString()
        {
            return Weight >= 0
                ? $"{Name} +{Weight}"
                : $"{Name} {Weight}"
                ;
        }
    }
}
