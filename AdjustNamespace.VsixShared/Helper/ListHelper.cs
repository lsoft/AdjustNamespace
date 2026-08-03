using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Helper
{
    /// <summary>
    /// A set of small LINQ-like extensions used across the extension codebase.
    /// </summary>
    public static class ListHelper
    {
        /// <summary>
        /// The n-th (zero based) item of the sequence.
        /// </summary>
        public static T Nth<T>(this IEnumerable<T> c, int n)
        {
            return c.Skip(n).First();
        }

        /// <summary>
        /// The n-th (zero based) item of the sequence, or the default value if there is no such item.
        /// </summary>
        public static T? NThOrDefault<T>(this IEnumerable<T> c, int n)
        {
            return c.Skip(n).FirstOrDefault();
        }

        /// <summary>
        /// The second item of the sequence.
        /// </summary>
        public static T Second<T>(this IEnumerable<T> c)
        {
            return c.Skip(1).First();
        }

        /// <summary>
        /// The second item of the sequence, or the default value if there is no such item.
        /// </summary>
        public static T? SecondOrDefault<T>(this IEnumerable<T> c)
        {
            return c.Skip(1).FirstOrDefault();
        }

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
        /// The items which do NOT match the predicate.
        /// </summary>
        public static IReadOnlyList<T> RemoveAll<T>(
            this IEnumerable<T> source,
            Func<T, bool> selector
            )
        {
            var list = new List<T>();

            foreach (var s in source)
            {
                if (selector(s))
                {
                    continue;
                }

                list.Add(s);
            }

            return list;
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
        /// Flatten the sequence of sequences into a single list (LINQ SelectMany analogue).
        /// </summary>
        public static List<T1> Collapse<T1, T2>(
            this IEnumerable<T2> list,
            Func<T2, IEnumerable<T1>> converter
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

            var result = new List<T1>();

            foreach (var a in list)
            {
                foreach (var b in converter(a))
                {
                    result.Add(b);
                }
            }

            return result;
        }

        /// <summary>
        /// A shuffled copy of the sequence.
        /// </summary>
        public static List<T> Shuffle<T>(
            this IEnumerable<T> list
            )
        {
            var rnd = new Random(
                BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0)
                );

            var result = new List<T>(list);

            for (var i = 0; i < result.Count - 1; i++)
            {
                if (rnd.Next() >= 0.5f)
                {
                    var newIndex = rnd.Next(result.Count);

                    var tmp = result[i];
                    result[i] = result[newIndex];
                    result[newIndex] = tmp;
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
        /// Convert every item to a string and join them with the separator
        /// (with <see cref="Environment.NewLine"/> if no separator is given).
        /// </summary>
        public static string Join<T>(
            this IEnumerable<T> list,
            Func<T, string> converter,
            string? separator = null
            )
        {
            if (separator is null)
            {
                separator = Environment.NewLine;
            }

            return string.Join(separator, list.Select(a => converter(a)));
        }

        /// <summary>
        /// The value is not in the given sequence.
        /// </summary>
        public static bool NotIn<T>(
            this T v,
            IEnumerable<T> array
            )
        {
            return
                !array.Contains(v);
        }

        /// <summary>
        /// The value is in the given sequence.
        /// </summary>
        public static bool In<T>(
            this T v,
            IEnumerable<T> array
            )
        {
            return
                array.Contains(v);
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
                !array.Contains(v);
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
                array.Contains(v);
        }

        /// <summary>
        /// The value is not in the given set of values (compared with the given comparer).
        /// </summary>
        public static bool NotIn<T>(
            this T v,
            IEqualityComparer<T> comparer,
            params T[] array
            )
        {
            return
                !array.Contains(v, comparer);
        }

        /// <summary>
        /// The value is in the given set of values (compared with the given comparer).
        /// </summary>
        public static bool In<T>(
            this T v,
            IEqualityComparer<T> comparer,
            params T[] array
            )
        {
            return
                array.Contains(v, comparer);
        }

        /// <summary>
        /// Split the enumerator into the batches of the given size
        /// (the last batch may be smaller).
        /// </summary>
        /// <exception cref="ArgumentException">The batch size is not positive.</exception>
        public static IEnumerable<List<T>> Split<T>(
            this IEnumerator<T> list,
            int splitCount
            )
        {
            if (splitCount <= 0)
            {
                throw new ArgumentException("splitCount <= 0");
            }

            var nextList = new List<T>();

            while (list.MoveNext())
            {
                var item = list.Current;

                nextList.Add(item);

                if (nextList.Count == splitCount)
                {
                    yield return nextList;

                    nextList = new List<T>();
                }
            }

            //if (list.Count % splitCount != 0)
            if (nextList.Count > 0)
            {
                yield return nextList;
            }
        }

        /// <summary>
        /// Split the sequence into the batches of the given size
        /// (the last batch may be smaller).
        /// </summary>
        /// <exception cref="ArgumentException">The batch size is not positive.</exception>
        public static IEnumerable<List<T>> Split<T>(
            this IEnumerable<T> list,
            int splitCount
            )
        {
            if (splitCount <= 0)
            {
                throw new ArgumentException("splitCount <= 0");
            }

            var nextList = new List<T>();

            foreach (var item in list)
            {
                nextList.Add(item);

                if (nextList.Count == splitCount)
                {
                    yield return nextList;

                    nextList = new List<T>();
                }
            }

            //if (list.Count % splitCount != 0)
            if (nextList.Count > 0)
            {
                yield return nextList;
            }
        }

    }

}
