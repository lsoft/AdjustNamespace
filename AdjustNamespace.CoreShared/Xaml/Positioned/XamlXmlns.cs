using System;

namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// A CLR namespace mapping in a xaml document:
    /// <c>xmlns:alias="clr-namespace:A.B.C;assembly=D"</c> (WPF / classic)
    /// or <c>xmlns:alias="using:A.B.C"</c> (Avalonia / UWP-style, preferred in Avalonia).
    /// </summary>
    public class XamlXmlns : IXamlPositioned
    {
        /// <summary>
        /// The WPF-style form of the attribute value.
        /// </summary>
        public const string ClrNamespaceForm = "clr-namespace";

        /// <summary>
        /// The UWP / Avalonia style form of the attribute value.
        /// </summary>
        public const string UsingForm = "using";

        /// <inheritdoc/>
        /// <remarks>
        /// Meaningful for the declarations read from the document only
        /// (i.e. when <see cref="Saved"/> is <c>true</c>); a newly created declaration
        /// is inserted at the end of the existing ones, see <see cref="SaveTo"/>.
        /// </remarks>
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
        /// Alias of the declaration.
        /// </summary>
        public string Alias
        {
            get;
        }

        /// <summary>
        /// The declared clr namespace.
        /// </summary>
        public string Namespace
        {
            get;
        }

        /// <summary>
        /// This declaration is in the document body already.
        /// A newly created one (<c>false</c>) has to be written into the body, see <see cref="SaveTo"/>.
        /// </summary>
        public bool Saved
        {
            get;
        }

        /// <summary>
        /// Everything which follows the namespace inside the attribute value,
        /// e.g. <c>;assembly=D</c>. It is inherited by the newly created declarations.
        /// </summary>
        public string Suffix
        {
            get;
        }

        /// <summary>
        /// <see cref="ClrNamespaceForm"/> or <see cref="UsingForm"/>: the syntax the
        /// declaration was written with (and the one a newly created sibling keeps).
        /// </summary>
        public string Form
        {
            get;
        }

        /// <summary>
        /// Create a declaration which has been read from the document body.
        /// </summary>
        public XamlXmlns(
            int index,
            int length,
            string alias,
            string @namespace,
            string suffix,
            string form = ClrNamespaceForm
            )
        {
            Index = index;
            Length = length;
            Alias = alias;
            Namespace = @namespace;
            Saved = true;
            Suffix = suffix;
            Form = form;
        }

        /// <summary>
        /// Create a new declaration for the target namespace, based on the declaration
        /// of the source namespace (to inherit its <see cref="Suffix"/> and <see cref="Form"/>).
        /// The alias is generated from the last part of the namespace plus a part of a guid
        /// to prevent a collision with the existing aliases.
        /// </summary>
        /// <param name="xmlns">Declaration of the source namespace.</param>
        /// <param name="targetNamespace">The namespace to declare.</param>
        public XamlXmlns(
            XamlXmlns xmlns,
            string targetNamespace
            )
        {
            Index = xmlns.Index + Length;
            Length = 0;
            Alias = GetLastWord(targetNamespace) + GetPartOfGuid();
            Namespace = targetNamespace;
            Saved = false;
            Suffix = xmlns.Suffix;
            Form = xmlns.Form;
        }

        /// <summary>
        /// Write this declaration into the document body.
        /// </summary>
        /// <param name="xaml">(in/out) Body of the xaml document.</param>
        /// <param name="indexToInsert">(in/out) Position to insert at; it is moved behind the inserted text.</param>
        internal void SaveTo(ref string xaml, ref int indexToInsert)
        {
            var s = $@" xmlns:{Alias}=""{Form}:{Namespace}{Suffix}""";
            xaml = xaml.Insert(indexToInsert, s);
            indexToInsert += s.Length;
        }

        /// <summary>
        /// Cut this declaration out of the document body.
        /// </summary>
        internal void Remove(ref string xaml)
        {
            xaml = xaml.Substring(0, Index) + xaml.Substring(Index + Length);
        }

        /// <summary>
        /// The part of the namespace after the last dot.
        /// </summary>
        private string GetLastWord(string s)
        {
            if (s.Contains("."))
            {
                return s.Substring(s.LastIndexOf('.') + 1);
            }

            return s;
        }

        /// <summary>
        /// The first group of a fresh guid; used to make the generated alias unique.
        /// </summary>
        private string GetPartOfGuid()
        {
            var g = Guid.NewGuid().ToString();
            g = g.Substring(0, g.IndexOf('-'));

            return g;
        }

    }
}
