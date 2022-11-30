using System;

namespace AdjustNamespace.Xaml.Positioned
{
    public class XamlControl : IXamlPerformable
    {
        public int Index
        {
            get;
        }
        public int Length
        {
            get;
        }
        public string TagPrefix
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

        public XamlControl(
            int index,
            int length,
            string tagPrefix,
            string alias,
            string className
            )
        {
            Index = index;
            Length = length;
            TagPrefix = tagPrefix;
            Alias = alias;
            ClassName = className;
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

            xaml = xaml.Substring(0, Index)
                + $"<{TagPrefix}{targetXmlns.Alias}:{ClassName}"
                + xaml.Substring(Index + Length)
                ;
            return true;
        }
    }
}
