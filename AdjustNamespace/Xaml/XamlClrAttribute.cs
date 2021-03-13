using System;
using System.Xml.Linq;

namespace AdjustNamespace.Xaml
{
    public class XamlClrAttribute
    {
        public XAttribute Attribute
        {
            get;
        }
        public string XamlKey
        {
            get;
        }
        public string ClrNamespace
        {
            get;
        }

        public XamlClrAttribute(
            XAttribute attribute
            )
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            var clrAttributeNamespace = XamlHelper.GetNamespace(attribute.Value);

            //Debug.WriteLine(clrAttribute.Name + "  -->  " + clrAttributeNamespace);

            Attribute = attribute;
            XamlKey = attribute.Name.LocalName;
            ClrNamespace = clrAttributeNamespace;
        }

    }
}
