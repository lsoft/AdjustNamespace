using System;
using System.Xml.Linq;

namespace AdjustNamespace.Xaml
{
    public class XamlClrAttribute
    {
        public string Alias
        {
            get;
        }
        public string ClassName
        {
            get;
        }
        public XAttribute Attribute
        {
            get;
        }

        public XamlClrAttribute(
            string alias,
            string className,
            XAttribute attribute
            )
        {
            if (alias is null)
            {
                throw new ArgumentNullException(nameof(alias));
            }

            if (className is null)
            {
                throw new ArgumentNullException(nameof(className));
            }

            if (attribute is null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            Alias = alias;
            ClassName = className;
            Attribute = attribute;
        }
    }
}
