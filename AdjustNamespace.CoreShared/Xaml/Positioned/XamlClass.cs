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
        public XamlClass(
            int index,
            int length,
            string fullClassName
            )
        {
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

            var xPrefix = structure.GetXPrefix();

            xaml = xaml.Substring(0, Index)
                + $@"{xPrefix.Alias}:Class=""{targetNamespace}.{ClassName}"""
                + xaml.Substring(Index + Length)
                ;
            return true;
        }
    }
}
