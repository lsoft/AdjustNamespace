using AdjustNamespace.Xaml.Positioned;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Xaml
{
    /// <summary>
    /// Interesting structures from Xaml document.
    /// </summary>
    public readonly struct XamlStructure
    {
        /// <summary>
        /// Alias of the xaml language namespace (usually `x`).
        /// </summary>
        public readonly XamlX XPrefix;

        /// <summary>
        /// Every namespace declaration of the document, the CLR ones
        /// (<c>xmlns:alias="clr-namespace:A.B.C"</c>) and the other ones, with their scopes.
        /// </summary>
        public readonly List<XamlXmlns> Xmlns;

        /// <summary>
        /// Tags: <c>&lt;alias:ClassName</c>, and the tags without a prefix.
        /// </summary>
        public readonly List<XamlControl> Controls;

        /// <summary>
        /// Type references inside the markup extensions: <c>{x:Type alias:ClassName}</c>.
        /// </summary>
        public readonly List<XamlAttributeReference> RefFroms;

        /// <summary>
        /// <c>x:Class</c> attributes.
        /// </summary>
        public readonly List<XamlClass> Classes;

        /// <summary>
        /// The <c>alias:ClassName</c> pairs which are neither a tag nor a
        /// <c>{x:Type}</c>/<c>{x:Static}</c> markup extension: an attribute value,
        /// an attached property, a custom markup extension, <c>x:TypeArguments</c>,
        /// an Avalonia selector.
        /// </summary>
        public readonly List<XamlTypeUsage> TypeUsages;

        public XamlStructure(
            XamlX xPrefix,
            List<XamlXmlns> xmlns,
            List<XamlControl> controls,
            List<XamlAttributeReference> refFroms,
            List<XamlClass> classes,
            List<XamlTypeUsage> typeUsages
            )
        {
            XPrefix = xPrefix ?? throw new ArgumentNullException(nameof(xPrefix));
            Xmlns = xmlns ?? throw new ArgumentNullException(nameof(xmlns));
            Controls = controls ?? throw new ArgumentNullException(nameof(controls));
            RefFroms = refFroms ?? throw new ArgumentNullException(nameof(refFroms));
            Classes = classes ?? throw new ArgumentNullException(nameof(classes));
            TypeUsages = typeUsages ?? throw new ArgumentNullException(nameof(typeUsages));
        }

        /// <summary>
        /// Get the namespace declaration of the given alias which is visible at the given
        /// position: an alias declared again on a nested element means the nested declaration
        /// inside of that element.
        /// </summary>
        /// <param name="alias">The alias; empty for the default declaration.</param>
        /// <param name="position">The position the alias is used at.</param>
        /// <returns>
        /// <c>null</c> if there is no such alias at that position. A declaration which is no
        /// CLR namespace mapping is returned too (it shadows the outer ones), see <see cref="XamlXmlns.IsClr"/>.
        /// </returns>
        public XamlXmlns? GetByAlias(string alias, int position)
        {
            XamlXmlns? result = null;

            foreach (var xmlns in Xmlns)
            {
                if (xmlns.Alias != alias || !xmlns.IsInScopeAt(position))
                {
                    continue;
                }

                if (result == null || xmlns.ScopeStart > result.ScopeStart)
                {
                    result = xmlns;
                }
            }

            return result;
        }

        /// <summary>
        /// Try to find a declaration of the given CLR namespace within the given assembly
        /// which may be used at the given position.
        /// </summary>
        /// <param name="namespace">The clr namespace.</param>
        /// <param name="suffix">
        /// The rest of the attribute value, i.e. <c>;assembly=D</c> or an empty string
        /// for the assembly of the document itself. A declaration of the same namespace
        /// in another assembly points to another type and must not be taken;
        /// the comparison is a plain one, so an explicit <c>;assembly=</c> of the own
        /// assembly leads to a second declaration instead of a wrong reuse.
        /// </param>
        /// <param name="position">The position the alias is going to be used at: a declaration
        /// of a nested element is not visible outside of it, and a declaration whose alias is
        /// declared again in between is shadowed.</param>
        /// <returns><c>null</c> if there is no such declaration.</returns>
        public XamlXmlns? TryGetByNamespace(string @namespace, string suffix, int position)
        {
            foreach (var xmlns in Xmlns)
            {
                if (!xmlns.IsClr
                    || xmlns.Alias.Length == 0
                    || xmlns.Namespace != @namespace
                    || xmlns.Suffix != suffix
                    )
                {
                    continue;
                }

                if (!xmlns.Saved)
                {
                    //a new declaration is written into the root element
                    return xmlns;
                }

                if (ReferenceEquals(GetByAlias(xmlns.Alias, position), xmlns))
                {
                    return xmlns;
                }
            }

            return null;
        }

        /// <summary>
        /// Every tag the given default CLR declaration maps is a reference to the moved class
        /// (<c>&lt;MyButton xmlns="clr-namespace:A.B" /&gt;</c>): the declaration follows the
        /// class then instead of the tags getting a prefix.
        /// </summary>
        public bool IsDefaultDeclarationOfMovedClassOnly(XamlXmlns declaration, in XamlMove move)
        {
            var found = false;

            foreach (var control in Controls)
            {
                if (control.Alias.Length != 0
                    || !ReferenceEquals(GetByAlias(string.Empty, control.Index), declaration)
                    )
                {
                    continue;
                }

                if (!move.IsNamed(control.ClassName))
                {
                    return false;
                }

                found = true;
            }

            return found;
        }

        /// <summary>
        /// Alias of the xaml language namespace (usually `x`).
        /// </summary>
        public XamlX GetXPrefix()
        {
            return XPrefix;
        }

        /// <summary>
        /// All the places of the document which may reference a moved class.
        /// </summary>
        public List<IXamlPerformable> GetPerformables()
        {
            var performables = new List<IXamlPerformable>();

            performables.AddRange(Xmlns.Where(x => x.IsClr && x.Alias.Length == 0));
            performables.AddRange(Controls);
            performables.AddRange(RefFroms);
            performables.AddRange(Classes);
            performables.AddRange(TypeUsages);

            return performables;
        }

        /// <summary>
        /// Build a copy of this structure with an additional clr-namespace declaration.
        /// </summary>
        internal XamlStructure Add(XamlXmlns newXmlns)
        {
            var xmlns = new List<XamlXmlns>(Xmlns);
            xmlns.Add(newXmlns);

            return new XamlStructure(
                XPrefix,
                xmlns,
                Controls,
                RefFroms,
                Classes,
                TypeUsages
                );
        }
    }
}
