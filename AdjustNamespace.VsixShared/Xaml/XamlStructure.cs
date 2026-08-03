using AdjustNamespace.Xaml.Positioned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        /// clr-namespace declarations: <c>xmlns:alias="clr-namespace:A.B.C"</c>.
        /// </summary>
        public readonly List<XamlXmlns> Xmlns;

        /// <summary>
        /// Tags with an alias: <c>&lt;alias:ClassName</c>.
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

        public XamlStructure(
            XamlX xPrefix,
            List<XamlXmlns> xmlns,
            List<XamlControl> controls,
            List<XamlAttributeReference> refFroms,
            List<XamlClass> classes
            )
        {
            if (xPrefix is null)
            {
                throw new ArgumentNullException(nameof(xPrefix));
            }

            if (xmlns is null)
            {
                throw new ArgumentNullException(nameof(xmlns));
            }

            if (controls is null)
            {
                throw new ArgumentNullException(nameof(controls));
            }

            if (refFroms is null)
            {
                throw new ArgumentNullException(nameof(refFroms));
            }

            if (classes is null)
            {
                throw new ArgumentNullException(nameof(classes));
            }

            XPrefix = xPrefix;
            Xmlns = xmlns;
            Controls = controls;
            RefFroms = refFroms;
            Classes = classes;
        }

        /// <summary>
        /// Get the clr-namespace declaration by its alias.
        /// </summary>
        /// <returns>
        /// <c>null</c> if there is no such alias in the document: the alias may belong
        /// to a namespace which is not a clr one (<c>&lt;x:Array&gt;</c>, for example).
        /// </returns>
        public XamlXmlns? GetByAlias(string alias)
        {
            return Xmlns.FirstOrDefault(x => x.Alias == alias);
        }

        /// <summary>
        /// Try to find the clr-namespace declaration of the given namespace within
        /// the given assembly.
        /// </summary>
        /// <param name="namespace">The clr namespace.</param>
        /// <param name="suffix">
        /// The rest of the attribute value, i.e. <c>;assembly=D</c> or an empty string
        /// for the assembly of the document itself. A declaration of the same namespace
        /// in another assembly points to another type and must not be taken;
        /// the comparison is a plain one, so an explicit <c>;assembly=</c> of the own
        /// assembly leads to a second declaration instead of a wrong reuse.
        /// </param>
        /// <returns><c>null</c> if there is no such declaration in the document.</returns>
        public XamlXmlns? TryGetByNamespace(string @namespace, string suffix)
        {
            return Xmlns.FirstOrDefault(x => x.Namespace == @namespace && x.Suffix == suffix);
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

            performables.AddRange(Controls);
            performables.AddRange(RefFroms);
            performables.AddRange(Classes);

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
                Classes
                );
        }
    }
}
