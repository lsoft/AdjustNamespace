using System;
using System.Xml.Linq;

namespace AdjustNamespace.Xaml
{
    public class XamlClrObject
    {
        public XElement Element
        {
            get;
        }
        public string ClassName
        {
            get;
        }
        public string Namespace
        {
            get;
        }

        public XamlClrObject(XElement element)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));

            ClassName = element.Name.LocalName;
            Namespace = XamlHelper.GetNamespace(element.Name.Namespace.NamespaceName);
        }
    }
}
