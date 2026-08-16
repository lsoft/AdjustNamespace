using System;

namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// A tag which references a class through an xmlns alias: <c>&lt;alias:ClassName ...</c>.
    /// </summary>
    public class XamlControl : IXamlPerformable
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
        /// `/` for a closing tag, an empty string for an opening one.
        /// </summary>
        public string TagPrefix
        {
            get;
        }

        /// <summary>
        /// xmlns alias of the tag.
        /// </summary>
        public string Alias
        {
            get;
        }

        /// <summary>
        /// Name of the class (without the namespace).
        /// </summary>
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

            xaml = xaml.Substring(0, Index)
                + $"<{TagPrefix}{targetXmlns.Alias}:{ClassName}"
                + xaml.Substring(Index + Length)
                ;
            return true;
        }
    }
}
