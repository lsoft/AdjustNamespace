using System;

namespace AdjustNamespace.Xaml
{
    public class XamlAttributeReference : IXamlPerformable
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
            IXmlnsProvider xmlnsProvider,
            int index,
            int length,
            string prefix,
            string alias,
            string className
            )
        {
            if (xmlnsProvider == null)
                throw new ArgumentNullException(nameof(xmlnsProvider));

            _xmlnsProvider = xmlnsProvider;

            Index = index;
            Length = length;
            Alias = alias;
            ClassName = className;
            Prefix = prefix;
        }

        public bool Perform(
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

            var sourceXmlns = _xmlnsProvider.GetByAlias(Alias);
            if (sourceXmlns.Namespace != sourceNamespace)
            {
                return false;
            }

            //match!

            //get or create new xmlns
            var targetXmlns = _xmlnsProvider.TryGetByNamespace(targetNamespace);
            if (targetXmlns == null)
            {
                targetXmlns = new XamlXmlns(
                    sourceXmlns,
                    targetNamespace
                    );
                newXmlns = targetXmlns;
            }

            var xPrefix = _xmlnsProvider.GetXPrefix();

            xaml = xaml.Substring(0, Index)
                + $"{{{xPrefix.Alias}:{Prefix} {targetXmlns.Alias}:{ClassName}"
                + xaml.Substring(Index + Length)
                ;
            return true;
        }
    }
}
