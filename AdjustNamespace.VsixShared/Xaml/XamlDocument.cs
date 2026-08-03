using AdjustNamespace.Xaml.BodyProvider;
using AdjustNamespace.Xaml.Positioned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdjustNamespace.Xaml
{
    /// <summary>
    /// An immutable in-memory representation of a xaml file.
    /// Every modification produces a new instance; nothing is written back to the file
    /// until <see cref="SaveIfChangesExistsAgainst"/> is called.
    ///
    /// The xaml is processed as a plain text with a set of regexes instead of an XML DOM.
    /// This looks fragile, but it is the only way to keep the user's formatting untouched.
    /// </summary>
    public readonly struct XamlDocument
    {
        private readonly IXamlBodyProvider _bodyProvider;

        /// <summary>
        /// The whole body of the document.
        /// </summary>
        private readonly string _xaml;

        /// <summary>
        /// The interesting parts of the body (xmlns clauses, controls, x:Class etc.)
        /// with their positions in <see cref="_xaml"/>.
        /// </summary>
        private readonly XamlStructure _structure;

        /// <summary>
        /// Read the document body through the given provider and parse it.
        /// </summary>
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

        /// <summary>
        /// Build a new document in which every reference to the given class
        /// (a control tag, a <c>{x:Type}</c>/<c>{x:Static}</c> markup extension or
        /// the <c>x:Class</c> attribute) points to the target namespace.
        /// If the target namespace has no xmlns alias yet, a new alias is created;
        /// the aliases which became unused are removed.
        /// </summary>
        /// <param name="sourceNamespace">Namespace the class lives in now.</param>
        /// <param name="objectClassName">Name of the class (without the namespace).</param>
        /// <param name="targetNamespace">Namespace the class is being moved into.</param>
        /// <returns>The modified document, or this one if there is nothing to change.</returns>
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

            //the newly created xmlns aliases are not in the body yet;
            //append them after the last existing xmlns clause
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

        /// <summary>
        /// Get the namespace and the name of the root class of the document
        /// (the class from its <c>x:Class</c> attribute).
        /// </summary>
        /// <returns><c>false</c> if the document has no <c>x:Class</c> at all (a ResourceDictionary, for example).</returns>
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

        /// <summary>
        /// Check if this document differs from the given one.
        /// </summary>
        public bool IsChangesExists(XamlDocument source)
        {
            return source._xaml != this._xaml;
        }

        /// <summary>
        /// Write this document back to the file, but only if it differs from the given one.
        /// </summary>
        /// <param name="source">The document this one has been produced from.</param>
        public void SaveIfChangesExistsAgainst(XamlDocument source)
        {
            if (!IsChangesExists(source))
            {
                return;
            }

            _bodyProvider.UpdateText(_xaml);
        }

        /// <summary>
        /// Parse the interesting parts of the given xaml body.
        /// </summary>
        private static XamlStructure ReadStructure(string xaml)
        {
            var xPrefix = ReadXPrefix(xaml);
            var xmlns = ReadXmlns(xaml).ToList();
            var controls = ReadControls(xaml).ToList();
            var refFroms = ReadRefFromAttributes(xPrefix, xaml).ToList();
            var classes = ReadClasses(xPrefix, xaml).ToList();

            return new XamlStructure(xPrefix, xmlns, controls, refFroms, classes);
        }

        /// <summary>
        /// Find the type references inside the markup extensions:
        /// <c>{x:Type alias:ClassName}</c> and <c>{x:Static alias:ClassName}</c>.
        /// </summary>
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

        /// <summary>
        /// Determine the alias of the xaml language namespace (usually `x`, but it may be renamed).
        /// WPF and MAUI use different uris for it.
        /// </summary>
        private static XamlX ReadXPrefix(string xaml)
        {
            var matches = Regex.Matches(xaml, @"xmlns\s?:\s?([\w\d]+)\s?=\s?\""http:\/\/schemas\.microsoft\.com\/winfx\/2006\/xaml\""");
            if (matches.Count == 0)
            {
                //for maui
                matches = Regex.Matches(xaml, @"xmlns\s?:\s?([\w\d]+)\s?=\s?\""http:\/\/schemas\.microsoft\.com\/winfx\/2009\/xaml\""");
            }

            if (matches.Count == 0)
            {
                // there is no x definition (i.e. ResourceDictionary etc)
                return new XamlX(0, 0, "NO_X_ALIAS");
            }

            var match = matches[0];

            return new XamlX(
                match.Index,
                match.Length,
                match.Groups[1].Value
                );
        }

        /// <summary>
        /// Find the clr-namespace declarations: <c>xmlns:alias="clr-namespace:A.B.C"</c>.
        /// </summary>
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

        /// <summary>
        /// Find the opening and closing tags with an alias: <c>&lt;alias:ClassName</c>, <c>&lt;/alias:ClassName</c>.
        /// </summary>
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

        /// <summary>
        /// Find the <c>x:Class="A.B.ClassName"</c> attributes.
        /// </summary>
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

        /// <summary>
        /// Remove the xmlns clauses whose aliases are not used anymore.
        /// </summary>
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
