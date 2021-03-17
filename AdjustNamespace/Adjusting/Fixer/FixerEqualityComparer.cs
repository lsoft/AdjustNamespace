using System;
using System.Collections.Generic;

namespace AdjustNamespace.Adjusting.Fixer
{
    public class FixerEqualityComparer : IEqualityComparer<IFixer>
    {
        public static readonly FixerEqualityComparer Entity = new FixerEqualityComparer();

        public bool Equals(IFixer x, IFixer y)
        {
            if (x == null && y == null)
            {
                return true;
            }
            if (x != null && y == null)
            {
                return false;
            }
            if (x == null && y != null)
            {
                return false;
            }

            if (x!.GetType() != y!.GetType())
            {
                return false;
            }

            return x.UniqueKey.Equals(y.UniqueKey);
        }

        public int GetHashCode(IFixer obj)
        {
            return HashCode.Combine(
                obj?.GetType().GetHashCode() ?? 0,
                obj?.UniqueKey.GetHashCode() ?? 0
                );
        }
    }
}
