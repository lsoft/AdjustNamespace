using System;
using System.Collections.Generic;
using System.Text;

namespace AdjustNamespace.VsixShared.Helper
{
    internal static  class HashSetHelper
    {
        public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> list)
        {
            foreach(var i  in list)
            {
                set.Add(i);
            }
        }
    }
}
