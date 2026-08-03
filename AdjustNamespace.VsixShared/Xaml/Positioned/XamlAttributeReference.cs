using System;

namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// A type reference inside a markup extension: <c>{x:Type alias:ClassName}</c>
    /// or <c>{x:Static alias:ClassName...}</c>.
    /// </summary>
    public class XamlAttributeReference : IXamlPerformable
    {
        /// <inheritdoc/>
        public int Index
        {
            get;
        }

        /// <inheritdoc/>
        public int Length
        {
            get;
        }

        /// <summary>
        /// Kind of the markup extension: `Type` or `Static`.
        /// </summary>
        public string Prefix
        {
            get;
        }

        /// <summary>
        /// xmlns alias of the referenced class.
        /// </summary>
        public string Alias
        {
            get;
        }

        /// <summary>
        /// Name of the referenced class (without the namespace).
        /// </summary>
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

        /// <inheritdoc/>
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
            if (sourceXmlns == null || sourceXmlns.Namespace != sourceNamespace)
            {
                //the alias is unknown (it is not a clr-namespace one)
                //or it points to another namespace
                return false;
            }

            //match!

            //get or create new xmlns
            var targetXmlns = structure.TryGetByNamespace(targetNamespace, sourceXmlns.Suffix);
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
