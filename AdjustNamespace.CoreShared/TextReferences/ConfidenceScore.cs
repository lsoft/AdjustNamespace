using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The score of a match: the signals that produced it, their sum put into 0..100 and the
    /// level of it.
    /// </summary>
    public sealed class ConfidenceScore
    {
        /// <summary>
        /// The signals, in the order they were applied: the place first, the modifiers then.
        /// </summary>
        public IReadOnlyList<ConfidenceSignal> Signals
        {
            get;
        }

        /// <summary>
        /// The score, 0..100.
        /// </summary>
        public int Value
        {
            get;
        }

        /// <summary>
        /// The level of <see cref="Value"/>.
        /// </summary>
        public ConfidenceLevel Level => Confidence.LevelOf(Value);

        public ConfidenceScore(
            IReadOnlyList<ConfidenceSignal> signals
            )
        {
            if (signals is null)
            {
                throw new ArgumentNullException(nameof(signals));
            }

            Signals = signals;
            Value = Confidence.Clamp(signals.Sum(s => s.Weight));
        }

        /// <summary>
        /// The signals as a phrase: <c>CSharp string literal +60, assembly of the moved type +20</c>.
        /// </summary>
        public string Explain()
        {
            return string.Join(", ", Signals.Select(s => s.ToString()));
        }

        public override string ToString()
        {
            return $"{Value} ({Level})";
        }
    }
}
