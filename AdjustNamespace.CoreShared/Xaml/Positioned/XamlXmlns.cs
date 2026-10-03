using System;

namespace AdjustNamespace.Xaml.Positioned
{
    /// <summary>
    /// A namespace declaration of a xaml document: a CLR namespace mapping
    /// (<c>xmlns:alias="clr-namespace:A.B.C;assembly=D"</c> of WPF and MAUI,
    /// <c>xmlns:alias="using:A.B.C"</c> of Avalonia and UWP) or any other one
    /// (<c>xmlns="http://..."</c>), which matters only because it may shadow a mapping.
    ///
    /// A declaration is visible inside the element it is written on (its scope), and an
    /// alias declared again on a nested element means the nested declaration there.
    /// The default declaration (<c>xmlns="clr-namespace:A.B"</c>) has an empty alias and
    /// maps the tags without a prefix.
    /// </summary>
    public class XamlXmlns : IXamlPerformable
    {
        /// <summary>
        /// The WPF-style form of the attribute value.
        /// </summary>
        public const string ClrNamespaceForm = "clr-namespace";

        /// <summary>
        /// The UWP / Avalonia style form of the attribute value.
        /// </summary>
        public const string UsingForm = "using";

        private const string AssemblyKey = "assembly=";

        /// <inheritdoc/>
        /// <remarks>
        /// Meaningful for the declarations read from the document only
        /// (i.e. when <see cref="Saved"/> is <c>true</c>); a newly created declaration
        /// is inserted into the root element, see <see cref="SaveTo"/>.
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
        /// Alias of the declaration; empty for the default one (<c>xmlns="..."</c>).
        /// </summary>
        public string Alias
        {
            get;
        }

        /// <summary>
        /// The declared clr namespace, or the whole value of a declaration which is not
        /// a CLR namespace mapping (see <see cref="IsClr"/>).
        /// </summary>
        public string Namespace
        {
            get;
        }

        /// <summary>
        /// The declaration maps a CLR namespace.
        /// </summary>
        public bool IsClr
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
        /// The assembly named by the <see cref="Suffix"/> (<c>;assembly=D</c>), or <c>null</c>
        /// if there is none: the mapping points to the assembly of the document then.
        /// </summary>
        public string? Assembly
        {
            get;
        }

        /// <summary>
        /// The quote the value is written in.
        /// </summary>
        public char Quote
        {
            get;
        }

        /// <summary>
        /// The beginning of the element the declaration is written on.
        /// </summary>
        public int ScopeStart
        {
            get;
        }

        /// <summary>
        /// The end of the element the declaration is written on (behind its closing tag).
        /// </summary>
        public int ScopeEnd
        {
            get;
        }

        /// <summary>
        /// Create a declaration which has been read from the document body.
        /// </summary>
        /// <param name="index">Index of the attribute in the body.</param>
        /// <param name="length">Length of the attribute.</param>
        /// <param name="alias">The alias, empty for the default declaration.</param>
        /// <param name="value">The value of the attribute, without the quotes.</param>
        /// <param name="quote">The quote the value is written in.</param>
        /// <param name="scopeStart">The beginning of the element the declaration is written on.</param>
        /// <param name="scopeEnd">The end of that element.</param>
        public XamlXmlns(
            int index,
            int length,
            string alias,
            string value,
            char quote,
            int scopeStart,
            int scopeEnd
            )
        {
            Index = index;
            Length = length;
            Alias = alias;
            Saved = true;
            Quote = quote;
            ScopeStart = scopeStart;
            ScopeEnd = scopeEnd;

            var colonIndex = value.IndexOf(':');
            var form = colonIndex > 0 ? value.Substring(0, colonIndex).Trim() : string.Empty;

            if (form == ClrNamespaceForm || form == UsingForm)
            {
                var rest = value.Substring(colonIndex + 1);
                var semicolonIndex = rest.IndexOf(';');

                IsClr = true;
                Form = form;
                Namespace = (semicolonIndex >= 0 ? rest.Substring(0, semicolonIndex) : rest).Trim();
                Suffix = semicolonIndex >= 0 ? rest.Substring(semicolonIndex) : string.Empty;
                Assembly = ReadAssembly(Suffix);
            }
            else
            {
                IsClr = false;
                Form = string.Empty;
                Namespace = value;
                Suffix = string.Empty;
                Assembly = null;
            }
        }

        /// <summary>
        /// Create a new declaration for the target namespace, based on the declaration
        /// of the source namespace (to inherit its <see cref="Suffix"/> and <see cref="Form"/>).
        /// The alias is generated from the last part of the namespace plus a part of a guid
        /// to prevent a collision with the existing aliases. It is declared on the root element,
        /// so it is visible in the whole document.
        /// </summary>
        /// <param name="xmlns">Declaration of the source namespace.</param>
        /// <param name="targetNamespace">The namespace to declare.</param>
        public XamlXmlns(
            XamlXmlns xmlns,
            string targetNamespace
            )
        {
            Index = xmlns.Index;
            Length = 0;
            Alias = GetLastWord(targetNamespace) + GetPartOfGuid();
            Namespace = targetNamespace;
            IsClr = true;
            Saved = false;
            Suffix = xmlns.Suffix;
            Form = xmlns.Form;
            Assembly = xmlns.Assembly;
            Quote = '"';
            ScopeStart = 0;
            ScopeEnd = int.MaxValue;
        }

        /// <summary>
        /// The declaration is visible at the given position of the body.
        /// </summary>
        public bool IsInScopeAt(int position)
        {
            return ScopeStart <= position && position < ScopeEnd;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Only the default CLR declaration (<c>xmlns="clr-namespace:A.B"</c>) is rewritten here,
        /// and only when every tag it maps is the moved class: the declaration then follows the
        /// class as a whole. Otherwise the tags of the moved class get a prefix, see
        /// <see cref="XamlControl"/>.
        /// </remarks>
        public bool Perform(
            XamlStructure structure,
            in XamlMove move,
            ref string xaml,
            out XamlXmlns? newXmlns
            )
        {
            newXmlns = null;

            if (Alias.Length != 0 || !Saved || !move.IsMappedBy(this))
            {
                return false;
            }

            if (!structure.IsDefaultDeclarationOfMovedClassOnly(this, move))
            {
                return false;
            }

            xaml = xaml.Substring(0, Index)
                + $"xmlns={Quote}{Form}:{move.TargetNamespace}{Suffix}{Quote}"
                + xaml.Substring(Index + Length)
                ;
            return true;
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
        /// The assembly of a <c>;assembly=D</c> suffix, if any.
        /// </summary>
        private static string? ReadAssembly(string suffix)
        {
            foreach (var part in suffix.Split(';'))
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith(AssemblyKey, StringComparison.OrdinalIgnoreCase))
                {
                    var assembly = trimmed.Substring(AssemblyKey.Length).Trim();
                    return assembly.Length == 0 ? null : assembly;
                }
            }

            return null;
        }

        /// <summary>
        /// The part of the namespace after the last dot.
        /// </summary>
        private static string GetLastWord(string s)
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
        private static string GetPartOfGuid()
        {
            var g = Guid.NewGuid().ToString();
            g = g.Substring(0, g.IndexOf('-'));

            return g;
        }
    }
}
