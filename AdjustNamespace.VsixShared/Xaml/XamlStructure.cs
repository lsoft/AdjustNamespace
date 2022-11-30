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
        public readonly XamlX XPrefix;
        public readonly List<XamlXmlns> Xmlns;
        public readonly List<XamlControl> Controls;
        public readonly List<XamlAttributeReference> RefFroms;
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

        public XamlXmlns GetByAlias(string alias)
        {
            return Xmlns.First(x => x.Alias == alias);
        }

        public XamlXmlns? TryGetByNamespace(string @namespace)
        {
            return Xmlns.FirstOrDefault(x => x.Namespace == @namespace);
        }

        public XamlX GetXPrefix()
        {
            return XPrefix;
        }

        public List<IXamlPerformable> GetPerformables()
        {
            var performables = new List<IXamlPerformable>();

            performables.AddRange(Controls);
            performables.AddRange(RefFroms);
            performables.AddRange(Classes);

            return performables;
        }

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
