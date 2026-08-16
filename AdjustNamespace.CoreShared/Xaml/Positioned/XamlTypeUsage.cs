using System;

namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// A bare <c>alias:ClassName</c> pair which is neither a tag nor an
    /// <c>{x:Type}</c>/<c>{x:Static}</c> markup extension. Xaml writes such a pair
    /// in a lot of places:
    /// <list type="bullet">
    /// <item>an attribute value: <c>TargetType="local:MyButton"</c>, <c>DataType="local:Item"</c>;</item>
    /// <item>an attached property: <c>&lt;Button attached:Helper.IsEnabled="True" /&gt;</c>;</item>
    /// <item>a custom markup extension: <c>{conv:UpperCase}</c>;</item>
    /// <item>the type arguments of a generic control: <c>x:TypeArguments="local:Item"</c>.</item>
    /// </list>
    /// All of them are references to a class and have to follow it into its new namespace.
    /// </summary>
    public class XamlTypeUsage : IXamlPerformable
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
        /// xmlns alias of the referenced class.
        /// </summary>
        public string Alias
        {
            get;
        }

        /// <summary>
        /// The name as it is written in the document (without the namespace).
        /// </summary>
        public string ClassName
        {
            get;
        }

        /// <summary>
        /// This pair is written inside the curly braces of a markup extension
        /// (<c>{conv:UpperCase}</c>).
        /// </summary>
        /// <remarks>
        /// The name of a markup extension may be written without the <c>Extension</c>
        /// suffix of its class, so <c>{conv:UpperCase}</c> is a reference to
        /// <c>UpperCase</c> as well as to <c>UpperCaseExtension</c>.
        /// </remarks>
        public bool IsMarkupExtension
        {
            get;
        }

        public XamlTypeUsage(
            int index,
            int length,
            string alias,
            string className,
            bool isMarkupExtension
            )
        {
            Index = index;
            Length = length;
            Alias = alias;
            ClassName = className;
            IsMarkupExtension = isMarkupExtension;
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

            if (!IsReferenceTo(objectClassName))
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

            //the name itself is written back as the user has written it:
            //a markup extension may be named without the `Extension` suffix of its class
            xaml = xaml.Substring(0, Index)
                + $"{targetXmlns.Alias}:{ClassName}"
                + xaml.Substring(Index + Length)
                ;
            return true;
        }

        /// <summary>
        /// This pair references the class with the given name.
        /// </summary>
        private bool IsReferenceTo(string objectClassName)
        {
            if (ClassName == objectClassName)
            {
                return true;
            }

            //`{conv:UpperCase}` is a reference to `UpperCaseExtension` as well
            return IsMarkupExtension && objectClassName == ClassName + "Extension";
        }
    }
}
