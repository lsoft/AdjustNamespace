using AdjustNamespace.TextReferences;
using System.Collections.Generic;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// The types the corpus tests of the score move: <c>Legacy.Models.Customer</c> and the like
    /// become <c>Lib.Models.Customer</c>.
    /// </summary>
    internal static class TextReferenceCorpus
    {
        public static readonly MovedType Customer = new("Legacy.Models", "Lib.Models", "Customer", 0, "Lib");

        public static readonly MovedType Order = new(
            "Legacy.Models",
            "Lib.Models",
            "Order",
            0,
            "Lib",
            nestedTypeNames: new[] { "Item", "Item+Part" }
            );

        public static readonly MovedType Repo = new("Legacy.Models", "Lib.Models", "Repo", 1, "Lib");

        /// <summary>
        /// A type that has a namesake in another assembly.
        /// </summary>
        public static readonly MovedType Clock = new(
            "Legacy.Util",
            "Lib.Util",
            "Clock",
            0,
            "Lib",
            hasLookalikeElsewhere: true
            );

        /// <summary>
        /// <c>Legacy.Models</c> is emptied and goes to <c>Lib.Models</c> as a whole;
        /// <c>Legacy.Util</c> is split between two places; <c>Legacy.Alive</c> keeps its types.
        /// </summary>
        public static MovedTypeSet Set()
        {
            return new MovedTypeSet(
                new[] { Customer, Order, Repo, Clock },
                new List<MovedNamespace>
                {
                    new("Legacy.Models", new[] { "Lib.Models" }, isEmptied: true),
                    new("Legacy.Util", new[] { "Lib.Util", "Lib.Other" }, isEmptied: true),
                    new("Legacy.Alive", new[] { "Lib.Alive" }, isEmptied: false)
                }
                );
        }
    }
}
