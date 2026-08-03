using System;
using System.Collections.Generic;
using System.Text;

namespace AdjustNamespace.Helper
{
    /// <summary>
    /// Helpers for <see cref="HashSet{T}"/>.
    /// </summary>
    internal static  class HashSetHelper
    {
        /// <summary>
        /// Add all the items of the list into the set (the duplicates are dropped silently).
        /// </summary>
        public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> list)
        {
            foreach(var i  in list)
            {
                set.Add(i);
            }
        }
    }
}
