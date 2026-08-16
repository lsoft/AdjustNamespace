using System;
using System.Collections.Generic;

namespace AdjustNamespace
{
    /// <summary>
    /// The few collection extensions the extension really uses. They live in the root
    /// namespace on purpose: every file of the project sees them without a `using`.
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// The items which match the predicate.
        /// </summary>
        public static IReadOnlyList<T> FindAll<T>(
            this IReadOnlyList<T> list,
            Func<T, bool> predicate
            )
        {
            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (predicate is null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            var result = new List<T>();

            foreach (var i in list)
            {
                if (predicate(i))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        /// <summary>
        /// Convert every item of the list (LINQ Select analogue with a preallocated result).
        /// </summary>
        public static List<T2> ConvertAll<T1, T2>(
            this IReadOnlyList<T1> list,
            Func<T1, T2> converter
            )
        {
            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (converter is null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            var result = new List<T2>(list.Count);

            for (var a = 0; a < list.Count; a++)
            {
                result.Add(converter(list[a]));
            }

            return result;
        }

        /// <summary>
        /// Perform the action for every item of the sequence.
        /// </summary>
        public static void ForEach<T>(
            this IEnumerable<T> list,
            Action<T> action
            )
        {
            if (list is null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            foreach (var a in list)
            {
                action(a);
            }
        }

        /// <summary>
        /// Add all the items of the sequence into the set (the duplicates are dropped silently).
        /// </summary>
        public static void AddRange<T>(
            this HashSet<T> set,
            IEnumerable<T> list
            )
        {
            foreach (var i in list)
            {
                set.Add(i);
            }
        }

        /// <summary>
        /// The value is in the given set of values.
        /// </summary>
        public static bool In<T>(
            this T v,
            params T[] array
            )
        {
            return
                Array.IndexOf(array, v) >= 0;
        }

        /// <summary>
        /// The value is not in the given set of values.
        /// </summary>
        public static bool NotIn<T>(
            this T v,
            params T[] array
            )
        {
            return
                Array.IndexOf(array, v) < 0;
        }
    }
}
