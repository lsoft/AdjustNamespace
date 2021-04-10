using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassLibrary2
{
    public static class MyHelper
    {
        public static bool In<T>(
            this T v,
            IEnumerable<T> array
            )
        {
            return
                array.Contains(v);
        }
    }
}
