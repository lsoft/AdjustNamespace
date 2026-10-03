using System;

namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// The <c>x:Class="A.B.ClassName"</c> attribute, i.e. the binding between
    /// the xaml file and its code behind class.
    /// </summary>
    public class XamlClass : IXamlPerformable
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
        /// Namespace part of the class name. Empty if the class has no namespace.
        /// </summary>
        public string Namespace
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

        /// <param name="index">Index of the attribute in the xaml body.</param>
        /// <param name="length">Length of the attribute.</param>
        /// <param name="fullClassName">Value of the attribute (the full class name).</param>
        /// <param name="quote">The quote the value is written in.</param>
        public XamlClass(
            int index,
            int length,
            string fullClassName,
            char quote = '"'
            )
        {
            Index = index;
            Length = length;
            Quote = quote;

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

        /// <inheritdoc/>
        /// <summary>
        /// The quote the value is written in; it is written back as it is.
        /// </summary>
        public char Quote
        {
            get;
        }

        /// <inheritdoc/>
        public bool Perform(
            XamlStructure structure,
            in XamlMove move,
            ref string xaml,
            out XamlXmlns? newXmlns
            )
        {
            newXmlns = null;

            if (ClassName != move.ClassName)
            {
                return false;
            }

            if (Namespace.Length == 0 || Namespace != move.SourceNamespace)
            {
                //a class of the global namespace is never moved: its code behind
                //has no namespace declaration to move
                return false;
            }

            //match!

            var xPrefix = structure.GetXPrefix();

            xaml = xaml.Substring(0, Index)
                + $"{xPrefix.Alias}:Class={Quote}{move.TargetNamespace}.{ClassName}{Quote}"
                + xaml.Substring(Index + Length)
                ;
            return true;
        }
    }
}
