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
    /// <item>the type arguments of a generic control: <c>x:TypeArguments="local:Item"</c>;</item>
    /// <item>an Avalonia style selector, which separates the alias with a <c>|</c>:
    /// <c>Selector="local|MyButton:pointerover"</c>.</item>
    /// </list>
    /// All of them are references to a class and have to follow it into its new namespace.
    /// </summary>
    public class XamlTypeUsage : IXamlPerformable
    {
        /// <summary>
        /// The separator of an ordinary xaml name.
        /// </summary>
        public const char NameSeparator = ':';

        /// <summary>
        /// The separator of an Avalonia style selector.
        /// </summary>
        public const char SelectorSeparator = '|';

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

        /// <summary>
        /// <see cref="NameSeparator"/> or <see cref="SelectorSeparator"/>: the character between
        /// the alias and the name, which is written back as it is.
        /// </summary>
        public char Separator
        {
            get;
        }

        public XamlTypeUsage(
            int index,
            int length,
            string alias,
            string className,
            bool isMarkupExtension,
            char separator = NameSeparator
            )
        {
            Index = index;
            Length = length;
            Alias = alias;
            ClassName = className;
            IsMarkupExtension = isMarkupExtension;
            Separator = separator;
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

            if (!IsReferenceTo(move))
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

            //the name itself is written back as the user has written it:
            //a markup extension may be named without the `Extension` suffix of its class
            xaml = xaml.Substring(0, Index)
                + $"{targetXmlns.Alias}{Separator}{ClassName}"
                + xaml.Substring(Index + Length)
                ;
            return true;
        }

        private bool IsReferenceTo(in XamlMove move)
        {
            if (ClassName == move.ClassName)
            {
                return true;
            }

            //`{conv:UpperCase}` is a reference to `UpperCaseExtension` as well
            return IsMarkupExtension && move.IsNamed(ClassName);
        }
    }
}
