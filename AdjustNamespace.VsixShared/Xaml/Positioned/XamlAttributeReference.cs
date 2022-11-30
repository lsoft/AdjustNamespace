using System;

namespace AdjustNamespace.Xaml.Positioned
{
    public class XamlAttributeReference : IXamlPerformable
    {
        public int Index
        {
            get;
        }
        public int Length
        {
            get;
        }
        public string Prefix
        {
            get;
        }
        public string Alias
        {
            get;
        }
        public string ClassName
        {
            get;
        }

        public XamlAttributeReference(
            int index,
            int length,
            string prefix,
            string alias,
            string className
            )
        {
            Index = index;
            Length = length;
            Alias = alias;
            ClassName = className;
            Prefix = prefix;
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
            if (sourceNamespace == null)
                throw new ArgumentNullException(nameof(sourceNamespace));

            if (objectClassName == null)
                throw new ArgumentNullException(nameof(objectClassName));

            if (targetNamespace == null)
                throw new ArgumentNullException(nameof(targetNamespace));

            if (xaml == null)
                throw new ArgumentNullException(nameof(xaml));

            newXmlns = null;

            if (ClassName != objectClassName)
            {
                return false;
            }

            var sourceXmlns = structure.GetByAlias(Alias);
            if (sourceXmlns.Namespace != sourceNamespace)
            {
                return false;
            }

            //match!

            //get or create new xmlns
            var targetXmlns = structure.TryGetByNamespace(targetNamespace);
            if (targetXmlns == null)
            {
                targetXmlns = new XamlXmlns(
                    sourceXmlns,
                    targetNamespace
                    );
                newXmlns = targetXmlns;
            }

            var xPrefix = structure.GetXPrefix();

            xaml = xaml.Substring(0, Index)
                + $"{{{xPrefix.Alias}:{Prefix} {targetXmlns.Alias}:{ClassName}"
                + xaml.Substring(Index + Length)
                ;
            return true;
        }
    }
}
