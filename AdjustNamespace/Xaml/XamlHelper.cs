using System;

namespace AdjustNamespace.Xaml
{
    public static class XamlHelper
    {
        public static string GetNamespace(string attributeValue)
        {
            if (attributeValue is null)
            {
                throw new ArgumentNullException(nameof(attributeValue));
            }

            var clrAttributeNamespace = attributeValue.Substring(XamlEngine.ClrNamespace.Length);
            var iof = clrAttributeNamespace.IndexOf(';');
            if (iof > 0)
            {
                clrAttributeNamespace = clrAttributeNamespace.Substring(0, iof);
            }

            return clrAttributeNamespace;
        }

    }
}
