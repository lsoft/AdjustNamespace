using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml
{
    public class XamlClass : IXamlPerformable
    {
        private readonly IXmlnsProvider _xmlnsProvider;

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
            IXmlnsProvider xmlnsProvider,
            int index,
            int length,
            string fullClassName
            )
        {
            if (xmlnsProvider == null)
                throw new ArgumentNullException(nameof(xmlnsProvider));

            _xmlnsProvider = xmlnsProvider;

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

            var xPrefix = _xmlnsProvider.GetXPrefix();

            xaml = xaml.Substring(0, Index)
                + $@"{xPrefix.Alias}:Class=""{targetNamespace}.{ClassName}"""
                + xaml.Substring(Index + Length)
                ;
            return true;
        }
    }
}
