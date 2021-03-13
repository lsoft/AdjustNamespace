using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml
{
    public class XamlClrAttributeNamespaceComparer : IEqualityComparer<XamlClrNamespace>
    {
        public static readonly XamlClrAttributeNamespaceComparer Instance = new XamlClrAttributeNamespaceComparer();

        public bool Equals(XamlClrNamespace x, XamlClrNamespace y)
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

            return x!.ClrNamespace == y!.ClrNamespace;
        }

        public int GetHashCode(XamlClrNamespace obj)
        {
            return obj?.ClrNamespace.GetHashCode() ?? 0;
        }
    }
}
