using System;

namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// A tag which references a class through an xmlns alias: <c>&lt;alias:ClassName ...</c>,
    /// or a tag without a prefix (<see cref="Alias"/> is empty), which references a class
    /// when the default declaration in scope maps a CLR namespace.
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
            in XamlMove move,
            ref string xaml,
            out XamlXmlns? newXmlns
            )
        {
            if (xaml == null)
                throw new ArgumentNullException(nameof(xaml));

            newXmlns = null;

            //a markup extension may be written as an element without its `Extension` suffix
            if (!move.IsNamed(ClassName))
            {
                return false;
            }

            var sourceXmlns = structure.GetByAlias(Alias, Index);
            if (!move.IsMappedBy(sourceXmlns))
            {
                //the alias is unknown (it is not a clr-namespace one), it points to another
                //namespace or to the same namespace of another assembly
                return false;
            }

            if (Alias.Length == 0
                && structure.IsDefaultDeclarationOfMovedClassOnly(sourceXmlns!, move))
            {
                //the default declaration itself follows the class, see XamlXmlns.Perform
                return false;
            }

            //match!

            //get or create new xmlns
            var targetXmlns = structure.TryGetByNamespace(move.TargetNamespace, sourceXmlns!.Suffix, Index);
            if (targetXmlns == null)
            {
                targetXmlns = new XamlXmlns(
                    sourceXmlns,
                    move.TargetNamespace
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
