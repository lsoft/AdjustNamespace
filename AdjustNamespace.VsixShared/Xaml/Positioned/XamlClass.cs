using System;

namespace AdjustNamespace.Xaml.Positioned
{
    public class XamlClass : IXamlPerformable
    {
        public int Index
        {
            get;
        }
        public int Length
        {
            get;
        }
        public string Namespace
        {
            get;
        }
        public string ClassName
        {
            get;
        }

        public XamlClass(
            int index,
            int length,
            string fullClassName
            )
        {
            Index = index;
            Length = length;

            var dotIndex = fullClassName.LastIndexOf('.');
            if (dotIndex > 0)
            {
                Namespace = fullClassName.Substring(0, dotIndex);
                ClassName = fullClassName.Substring(dotIndex + 1);
            }
            else
            {
                Namespace = string.Empty;
                ClassName = fullClassName;
            }
        }

        public bool Perform(
            XamlStructure structure,
            string sourceNamespace,
            string objectClassName,
            string targetNamespace,
            ref string xaml,
            out XamlXmlns? newXmlns
            )
        {
            newXmlns = null;

            if (ClassName != objectClassName)
            {
                return false;
            }

            if (Namespace != sourceNamespace)
            {
                return false;
            }

            //match!

            var xPrefix = structure.GetXPrefix();

            xaml = xaml.Substring(0, Index)
                + $@"{xPrefix.Alias}:Class=""{targetNamespace}.{ClassName}"""
                + xaml.Substring(Index + Length)
                ;
            return true;
        }
    }
}
