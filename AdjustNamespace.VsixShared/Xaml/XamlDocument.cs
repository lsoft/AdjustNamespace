using AdjustNamespace.Xaml.BodyProvider;
using AdjustNamespace.Xaml.Positioned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdjustNamespace.Xaml
{
    public readonly struct XamlDocument
    {
        private readonly IXamlBodyProvider _bodyProvider;
        private readonly string _xaml;
        private readonly XamlStructure _structure;

        public XamlDocument(
            IXamlBodyProvider bodyProvider
            ) : this(bodyProvider, bodyProvider.ReadText())
        {
        }

        private XamlDocument(
            IXamlBodyProvider bodyProvider,
            string xaml
            )
        {
            if (bodyProvider is null)
            {
                throw new ArgumentNullException(nameof(bodyProvider));
            }

            if (xaml is null)
            {
                throw new ArgumentNullException(nameof(xaml));
            }

            _bodyProvider = bodyProvider;
            _xaml = xaml;
            _structure = ReadStructure(xaml);
        }

        public XamlDocument MoveObject(
            string sourceNamespace,
            string objectClassName,
            string targetNamespace
            )
        {
            if (sourceNamespace is null)
            {
                throw new ArgumentNullException(nameof(sourceNamespace));
            }

            if (objectClassName is null)
            {
                throw new ArgumentNullException(nameof(objectClassName));
            }

            if (targetNamespace is null)
            {
                throw new ArgumentNullException(nameof(targetNamespace));
            }

            var xaml = _xaml;
            var structure = ReadStructure(xaml);
            var performables = structure.GetPerformables();

            //apply performables in backward order!
            var changesExists = false;
            foreach (var performable in performables.OrderByDescending(c => c.Index))
            {
                if (performable.Perform(
                    structure,
                    sourceNamespace,
                    objectClassName,
                    targetNamespace,
                    ref xaml,
                    out var newXmlns
                    ))
                {
                    changesExists = true;

                    if (newXmlns != null)
                    {
                        structure = structure.Add(newXmlns);
                    }
                }
            }

            if (!changesExists)
            {
                return this;
            }

            var reloadedXmlns = ReadXmlns(xaml).ToList();
            if (reloadedXmlns.Count > 0)
            {
                var indexToInsert = reloadedXmlns.Max(x => x.Index + x.Length);

                foreach (var xmlns in structure.Xmlns.Where(x => !x.Saved))
                {
                    xmlns.SaveTo(ref xaml, ref indexToInsert);
                }
            }

            Cleanup(ref xaml);

            return new XamlDocument(_bodyProvider, xaml);
        }

        public bool GetRootInfo(out string? rootNamespace, out string? rootName)
        {
            if (_structure.Classes.Count == 0)
            {
                rootNamespace = null;
                rootName = null;
                return false;
            }

            rootNamespace = _structure.Classes[0].Namespace;
            rootName = _structure.Classes[0].ClassName;
            return true;
        }

        public bool IsChangesExists(XamlDocument source)
        {
            return source._xaml != this._xaml;
        }

        public void SaveIfChangesExistsAgainst(XamlDocument source)
        {
            if (!IsChangesExists(source))
            {
                return;
            }

            _bodyProvider.UpdateText(_xaml);
        }

        private static XamlStructure ReadStructure(string xaml)
        {
            var xPrefix = ReadXPrefix(xaml);
            var xmlns = ReadXmlns(xaml).ToList();
            var controls = ReadControls(xaml).ToList();
            var refFroms = ReadRefFromAttributes(xPrefix, xaml).ToList();
            var classes = ReadClasses(xPrefix, xaml).ToList();

            return new XamlStructure(xPrefix, xmlns, controls, refFroms, classes);
        }

        private static IEnumerable<XamlAttributeReference> ReadRefFromAttributes(
            XamlX xPrefix,
            string xaml
            )
        {
            var matches0 = Regex.Matches(xaml, @$"{{\s?{xPrefix.Alias}:Type\s+([\w\d]+)\s?:\s?([\w\d]+)");
            foreach (Match match in matches0)
            {
                var xar = new XamlAttributeReference(
                    match.Index,
                    match.Length,
                    "Type",
                    match.Groups[1].Value,
                    match.Groups[2].Value
                    );

                yield return xar;
            }
            var matches1 = Regex.Matches(xaml, @$"{{\s?{xPrefix.Alias}:Static\s+([\w\d]+)\s?:\s?([\w\d]+)");
            foreach (Match match in matches1)
            {
                var xar = new XamlAttributeReference(
                    match.Index,
                    match.Length,
                    "Static",
                    match.Groups[1].Value,
                    match.Groups[2].Value
                    );

                yield return xar;
            }
        }

        private static XamlX ReadXPrefix(string xaml)
        {
            var matches = Regex.Matches(xaml, @"xmlns\s?:\s?([\w\d]+)\s?=\s?\""http:\/\/schemas\.microsoft\.com\/winfx\/2006\/xaml\""");
            var match = matches[0];

            return new XamlX(
                match.Index,
                match.Length,
                match.Groups[1].Value
                );
        }

        private static IEnumerable<XamlXmlns> ReadXmlns(
            string xaml
            )
        {
            var matches = Regex.Matches(xaml, @"xmlns:([\w\d]+)=""clr-namespace:([\w\d._]+)([^""]*)""");
            foreach (Match match in matches)
            {
                var xx = new XamlXmlns(
                    match.Index,
                    match.Length,
                    match.Groups[1].Value,
                    match.Groups[2].Value,
                    match.Groups[3].Value
                    );

                yield return xx;
            }
        }

        private static IEnumerable<XamlControl> ReadControls(
            string xaml
            )
        {
            var matches = Regex.Matches(xaml, @"<\s?(\/?)\s?([\w\d]+)\s?:\s?([\w\d]+)");
            foreach (Match match in matches)
            {
                var xc = new XamlControl(
                    match.Index,
                    match.Length,
                    match.Groups[1].Value,
                    match.Groups[2].Value,
                    match.Groups[3].Value
                    );

                yield return xc;
            }
        }

        private static IEnumerable<XamlClass> ReadClasses(
            XamlX xPrefix,
            string xaml
            )
        {
            var matches = Regex.Matches(xaml, @$"{xPrefix.Alias}:Class\s?=\s?""([\w\d._]+)""");
            foreach (Match match in matches)
            {
                var xc = new XamlClass(
                    match.Index,
                    match.Length,
                    match.Groups[1].Value
                    );

                yield return xc;
            }
        }

        private static void Cleanup(
            ref string xaml
            )
        {
            var r = ReadStructure(xaml);

            var aliases = new HashSet<string>();
            r.Controls.ForEach(c => aliases.Add(c.Alias));
            r.RefFroms.ForEach(c => aliases.Add(c.Alias));

            //in backward order!
            foreach (var xmln in r.Xmlns.OrderByDescending(x => x.Index))
            {
                if (aliases.Contains(xmln.Alias))
                {
                    continue;
                }

                xmln.Remove(ref xaml);
            }
        }
    }
}
