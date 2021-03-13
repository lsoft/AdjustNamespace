using System;

namespace AdjustNamespace.Xaml
{
    public class XamlReference
    {
        public string? Alias
        {
            get;
        }
        public string? Namespace
        {
            get;
        }

        public XamlReference(string? alias, string? @namespace)
        {
            Alias = alias;
            Namespace = @namespace;
        }

        internal bool DoesReferenceTo(XamlClrNamespace attribute)
        {
            if (attribute is null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            if (Alias != null)
            {
                if (Alias != attribute.XamlKey)
                {
                    return false;
                }
            }
            if (Namespace != null)
            {
                if (Namespace != attribute.ClrNamespace)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
