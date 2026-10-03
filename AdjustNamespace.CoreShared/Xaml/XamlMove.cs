using AdjustNamespace.Xaml.Positioned;
using System;

namespace AdjustNamespace.Xaml
{
    /// <summary>
    /// A single move of a class to another namespace, as the xaml engine sees it: what moves,
    /// where to, and which assembly the class and the document belong to.
    /// </summary>
    public readonly struct XamlMove
    {
        /// <summary>
        /// The namespace the class lives in now.
        /// </summary>
        public readonly string SourceNamespace;

        /// <summary>
        /// The name of the class (without the namespace).
        /// </summary>
        public readonly string ClassName;

        /// <summary>
        /// The namespace the class is moved into.
        /// </summary>
        public readonly string TargetNamespace;

        /// <summary>
        /// The assembly the class is compiled into, or <c>null</c> if it is unknown
        /// (then every mapping of <see cref="SourceNamespace"/> is taken as a mapping of it).
        /// </summary>
        public readonly string? TypeAssembly;

        /// <summary>
        /// The assembly the xaml document belongs to, or <c>null</c> if it is unknown.
        /// A <c>clr-namespace:</c> mapping without <c>;assembly=</c> points to this one.
        /// </summary>
        public readonly string? DocumentAssembly;

        /// <summary>
        /// The class may be written without its <c>Extension</c> suffix. <c>false</c> when the
        /// namespace has a class of that shorter name as well: xaml looks the name up as it is
        /// written first, so <c>&lt;local:Foo/&gt;</c> is that other class.
        /// </summary>
        public readonly bool AllowsShortExtensionName;

        public XamlMove(
            string sourceNamespace,
            string className,
            string targetNamespace,
            string? typeAssembly = null,
            string? documentAssembly = null,
            bool allowsShortExtensionName = true
            )
        {
            SourceNamespace = sourceNamespace ?? throw new ArgumentNullException(nameof(sourceNamespace));
            ClassName = className ?? throw new ArgumentNullException(nameof(className));
            TargetNamespace = targetNamespace ?? throw new ArgumentNullException(nameof(targetNamespace));
            TypeAssembly = typeAssembly;
            DocumentAssembly = documentAssembly;
            AllowsShortExtensionName = allowsShortExtensionName;
        }

        /// <summary>
        /// The name written in the document is the name of the moved class. A markup extension
        /// may be written without the <c>Extension</c> suffix of its class, both inside the
        /// curly braces (<c>{conv:UpperCase}</c>) and as an element (<c>&lt;conv:UpperCase/&gt;</c>).
        /// </summary>
        public bool IsNamed(string writtenName)
        {
            if (writtenName == ClassName)
            {
                return true;
            }

            return AllowsShortExtensionName
                && ClassName.Length > "Extension".Length
                && ClassName == writtenName + "Extension";
        }

        /// <summary>
        /// The mapping points to the namespace the class lives in now, in the assembly the
        /// class is compiled into: a namespace of the same name in another assembly is another
        /// namespace and its classes do not move.
        /// </summary>
        public bool IsMappedBy(XamlXmlns? xmlns)
        {
            if (xmlns == null || !xmlns.IsClr || xmlns.Namespace != SourceNamespace)
            {
                return false;
            }

            if (TypeAssembly == null)
            {
                return true;
            }

            if (xmlns.Form == XamlXmlns.UsingForm)
            {
                //`using:` names no assembly and is resolved among all the referenced ones
                return true;
            }

            var assembly = xmlns.Assembly ?? DocumentAssembly;
            if (assembly == null)
            {
                return true;
            }

            return string.Equals(assembly, TypeAssembly, StringComparison.OrdinalIgnoreCase);
        }
    }
}
