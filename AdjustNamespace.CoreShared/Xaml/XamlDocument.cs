using AdjustNamespace.Xaml.BodyProvider;
using AdjustNamespace.Xaml.Positioned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public readonly struct XamlDocument : IDisposable
    {
        /// <summary>
        /// An xml prefix (an xmlns alias): letters, digits, `_` and `-` (<c>my-ctrl</c>).
        /// </summary>
        private const string Prefix = @"[\w][\w\-]*";

        /// <summary>
        /// The name of a class as xaml writes it after the prefix.
        /// </summary>
        private const string ClassName = @"[\w]+";

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
        /// Build a new document in which every reference to the given class points to the
        /// target namespace, see <see cref="MoveObject(XamlMove)"/>. The assemblies are
        /// unknown here, so every mapping of the source namespace is taken.
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
            return MoveObject(
                new XamlMove(sourceNamespace, objectClassName, targetNamespace)
                );
        }

        /// <summary>
        /// Build a new document in which every reference to the given class
        /// (a control tag, a <c>{x:Type}</c>/<c>{x:Static}</c> markup extension, an attribute
        /// value, a selector or the <c>x:Class</c> attribute) points to the target namespace.
        /// If the target namespace has no xmlns alias yet, a new alias is declared on the
        /// root element; the aliases which became unused by this move are removed.
        /// </summary>
        /// <returns>The modified document, or this one if there is nothing to change.</returns>
        public XamlDocument MoveObject(
            XamlMove move
            )
        {
            var xaml = _xaml;
            var structure = ReadStructure(xaml);
            var usedBefore = ReadUsedDeclarationKeys(xaml, structure);
            var performables = structure.GetPerformables();

            //apply performables in backward order!
            var changesExists = false;
            foreach (var performable in performables.OrderByDescending(c => c.Index))
            {
                if (performable.Perform(
                    structure,
                    move,
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

            //the newly created xmlns aliases are not in the body yet: they are declared
            //on the root element, the only place which is visible from everywhere
            var newDeclarations = structure.Xmlns.Where(x => !x.Saved).ToList();
            if (newDeclarations.Count > 0)
            {
                var indexToInsert = FindRootDeclarationPosition(xaml, out var separator);
                if (indexToInsert >= 0)
                {
                    foreach (var xmlns in newDeclarations)
                    {
                        xmlns.SaveTo(ref xaml, ref indexToInsert, separator);
                    }
                }
            }

            Cleanup(ref xaml, usedBefore);

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
        /// Release the underlying body provider when it holds an (invisible) editor.
        /// Safe to call more than once; a file-system provider is a no-op.
        /// </summary>
        public void Dispose()
        {
            (_bodyProvider as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Parse the interesting parts of the given xaml body.
        /// </summary>
        private static XamlStructure ReadStructure(string xaml)
        {
            var comments = ReadCommentSpans(xaml);
            var elements = ReadElements(xaml);

            var xPrefix = ReadXPrefix(xaml, comments);
            var xmlns = ReadXmlns(xaml, comments, elements).ToList();
            var controls = ReadControls(xaml, comments).ToList();
            var refFroms = ReadRefFromAttributes(xPrefix, xaml, comments).ToList();
            var classes = ReadClasses(xPrefix, xaml, comments).ToList();
            var selectors = ReadSelectorUsages(xaml, comments).ToList();

            //everything which has been recognized already occupies its own piece of the body;
            //the rest of the `alias:Name` pairs is scanned afterwards
            var known = new List<IXamlPositioned>();
            known.Add(xPrefix);
            known.AddRange(xmlns);
            known.AddRange(controls);
            known.AddRange(refFroms);
            known.AddRange(classes);
            known.AddRange(selectors);

            var typeUsages = ReadTypeUsages(xaml, comments, known).ToList();
            typeUsages.AddRange(selectors);

            return new XamlStructure(xPrefix, xmlns, controls, refFroms, classes, typeUsages);
        }

        /// <summary>
        /// Find the <c>alias:ClassName</c> pairs which are not a part of anything recognized
        /// above: an attribute value (<c>TargetType="local:MyButton"</c>), an attached property
        /// (<c>attached:Helper.IsEnabled="True"</c>), a custom markup extension
        /// (<c>{conv:UpperCase}</c>) or <c>x:TypeArguments</c>.
        ///
        /// Everything which looks like such a pair is collected here, including the pairs which
        /// are no type references at all (<c>mc:Ignorable="d"</c>, a time in a text). They cost
        /// nothing: a pair is rewritten only if its alias is a clr-namespace one which points to
        /// the namespace the class is moved out of, see <see cref="XamlTypeUsage.Perform"/>.
        /// </summary>
        /// <param name="known">The fragments which are recognized already: the pairs inside of
        /// them are described by those fragments and must not be rewritten a second time.</param>
        private static IEnumerable<XamlTypeUsage> ReadTypeUsages(
            string xaml,
            List<(int Start, int End)> comments,
            List<IXamlPositioned> known
            )
        {
            var occupied = known
                .Where(k => k.Length > 0)
                .Select(k => (Start: k.Index, End: k.Index + k.Length))
                .ToList();

            //the whitespace around the `:` of an attribute is allowed by xml
            var matches = Regex.Matches(xaml, $@"({Prefix})\s*:\s*({ClassName})");
            foreach (Match match in matches)
            {
                if (IsCommented(comments, match.Index))
                {
                    continue;
                }

                var start = match.Index;
                var end = match.Index + match.Length;

                if (occupied.Any(o => start < o.End && end > o.Start))
                {
                    continue;
                }

                yield return new XamlTypeUsage(
                    start,
                    match.Length,
                    match.Groups[1].Value,
                    match.Groups[2].Value,
                    IsInsideMarkupExtension(xaml, start)
                    );
            }
        }

        /// <summary>
        /// Find the type references of the Avalonia style selectors, which separate the alias
        /// from the name with a <c>|</c>: <c>Selector="local|MyButton:pointerover"</c>,
        /// <c>Selector="Grid > :is(local|MyButton)"</c>.
        /// </summary>
        private static IEnumerable<XamlTypeUsage> ReadSelectorUsages(
            string xaml,
            List<(int Start, int End)> comments
            )
        {
            foreach (Match attribute in Regex.Matches(xaml, @"\bSelector\s*=\s*([""'])(.*?)\1", RegexOptions.Singleline))
            {
                if (IsCommented(comments, attribute.Index))
                {
                    continue;
                }

                var value = attribute.Groups[2];

                foreach (Match match in Regex.Matches(value.Value, $@"({Prefix})\|({ClassName})"))
                {
                    yield return new XamlTypeUsage(
                        value.Index + match.Index,
                        match.Length,
                        match.Groups[1].Value,
                        match.Groups[2].Value,
                        false,
                        XamlTypeUsage.SelectorSeparator
                        );
                }
            }
        }

        /// <summary>
        /// The given position is the very beginning of a markup extension (<c>{conv:UpperCase}</c>).
        /// </summary>
        private static bool IsInsideMarkupExtension(string xaml, int index)
        {
            var i = index - 1;

            while (i >= 0 && char.IsWhiteSpace(xaml[i]))
            {
                i--;
            }

            return i >= 0 && xaml[i] == '{';
        }

        /// <summary>
        /// Find the xaml comments (<c>&lt;!-- --&gt;</c>). The document is processed as a plain
        /// text, so the commented out markup has to be excluded from the parsing explicitly:
        /// otherwise it is modified as a real one.
        /// </summary>
        private static List<(int Start, int End)> ReadCommentSpans(string xaml)
        {
            var result = new List<(int Start, int End)>();

            foreach (Match match in Regex.Matches(xaml, @"<!--.*?-->", RegexOptions.Singleline))
            {
                result.Add((match.Index, match.Index + match.Length));
            }

            return result;
        }

        /// <summary>
        /// Is the given position inside a comment?
        /// </summary>
        private static bool IsCommented(List<(int Start, int End)> comments, int index)
        {
            foreach (var comment in comments)
            {
                if (index >= comment.Start && index < comment.End)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Find the elements of the document in the order of their start tags, with the span of
        /// every start tag and of every whole element. A namespace declaration is visible inside
        /// the element it is written on, so this is the scope of the declarations.
        ///
        /// The comments, the processing instructions and the CDATA sections are skipped, and a
        /// <c>&gt;</c> inside a quoted attribute value does not end a tag.
        /// </summary>
        private static List<XamlElement> ReadElements(string xaml)
        {
            var result = new List<XamlElement>();
            var open = new Stack<int>();

            var i = 0;
            while ((i = xaml.IndexOf('<', i)) >= 0)
            {
                if (string.CompareOrdinal(xaml, i, "<!--", 0, 4) == 0)
                {
                    i = SkipTo(xaml, i, "-->");
                    continue;
                }

                if (string.CompareOrdinal(xaml, i, "<![CDATA[", 0, 9) == 0)
                {
                    i = SkipTo(xaml, i, "]]>");
                    continue;
                }

                if (i + 1 < xaml.Length && (xaml[i + 1] == '?' || xaml[i + 1] == '!'))
                {
                    i = SkipTo(xaml, i, ">");
                    continue;
                }

                var tagEnd = FindTagEnd(xaml, i);

                if (i + 1 < xaml.Length && xaml[i + 1] == '/')
                {
                    if (open.Count > 0)
                    {
                        var index = open.Pop();
                        result[index] = result[index].ClosedAt(tagEnd);
                    }

                    i = tagEnd;
                    continue;
                }

                var isSelfClosing = tagEnd >= 2 && xaml[tagEnd - 2] == '/';

                result.Add(new XamlElement(i, tagEnd, isSelfClosing ? tagEnd : xaml.Length));
                if (!isSelfClosing)
                {
                    open.Push(result.Count - 1);
                }

                i = tagEnd;
            }

            return result;
        }

        /// <summary>
        /// The position behind the given terminator, or the end of the body.
        /// </summary>
        private static int SkipTo(string xaml, int start, string terminator)
        {
            var index = xaml.IndexOf(terminator, start + 1, StringComparison.Ordinal);

            return index < 0 ? xaml.Length : index + terminator.Length;
        }

        /// <summary>
        /// The position behind the <c>&gt;</c> of the tag which starts at the given position.
        /// </summary>
        private static int FindTagEnd(string xaml, int start)
        {
            char? quote = null;

            for (var i = start + 1; i < xaml.Length; i++)
            {
                var c = xaml[i];

                if (quote.HasValue)
                {
                    if (c == quote.Value)
                    {
                        quote = null;
                    }

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    quote = c;
                }
                else if (c == '>')
                {
                    return i + 1;
                }
            }

            return xaml.Length;
        }

        /// <summary>
        /// The position inside the start tag of the root element where a new namespace
        /// declaration is written: behind the last declaration of that tag, or behind the
        /// name of the element when it has none.
        /// </summary>
        /// <param name="xaml">Body of the xaml document.</param>
        /// <param name="separator">What a new declaration is written behind: a line break and
        /// the indentation of the last declaration when it is written on a line of its own,
        /// a space otherwise.</param>
        /// <returns><c>-1</c> if the document has no element at all.</returns>
        private static int FindRootDeclarationPosition(string xaml, out string separator)
        {
            separator = " ";

            var elements = ReadElements(xaml);
            if (elements.Count == 0)
            {
                return -1;
            }

            var root = elements[0];
            var startTag = xaml.Substring(root.Start, root.StartTagEnd - root.Start);

            var declarations = Regex.Matches(startTag, $@"(\s+)xmlns(\s*:\s*{Prefix})?\s*=\s*([""']).*?\3", RegexOptions.Singleline);
            if (declarations.Count > 0)
            {
                var last = declarations[declarations.Count - 1];

                var whiteSpace = last.Groups[1].Value;
                var lineBreakEnd = whiteSpace.LastIndexOf('\n');
                if (lineBreakEnd >= 0)
                {
                    var lineBreak = lineBreakEnd > 0 && whiteSpace[lineBreakEnd - 1] == '\r' ? "\r\n" : "\n";
                    separator = lineBreak + whiteSpace.Substring(lineBreakEnd + 1);
                }

                return root.Start + last.Index + last.Length;
            }

            var name = Regex.Match(startTag, @"<\s*[\w\-.:]+");
            return root.Start + name.Length;
        }

        /// <summary>
        /// Find the type references inside the markup extensions:
        /// <c>{x:Type alias:ClassName}</c> and <c>{x:Static alias:ClassName}</c>.
        /// </summary>
        private static IEnumerable<XamlAttributeReference> ReadRefFromAttributes(
            XamlX xPrefix,
            string xaml,
            List<(int Start, int End)> comments
            )
        {
            var x = Regex.Escape(xPrefix.Alias);

            foreach (var kind in new[] { "Type", "Static" })
            {
                var matches = Regex.Matches(xaml, $@"{{\s*{x}:{kind}\s+({Prefix})\s*:\s*({ClassName})");
                foreach (Match match in matches)
                {
                    if (IsCommented(comments, match.Index))
                    {
                        continue;
                    }

                    yield return new XamlAttributeReference(
                        match.Index,
                        match.Length,
                        kind,
                        match.Groups[1].Value,
                        match.Groups[2].Value
                        );
                }
            }
        }

        /// <summary>
        /// Determine the alias of the xaml language namespace (usually `x`, but it may be renamed).
        /// WPF and MAUI use different uris for it. A document which does not declare it at all
        /// still uses `x`: the implicit xmlns of .NET MAUI 10 declares it for the whole project.
        /// </summary>
        private static XamlX ReadXPrefix(string xaml, List<(int Start, int End)> comments)
        {
            foreach (var year in new[] { "2006", "2009" })
            {
                var match = FirstNotCommented(
                    Regex.Matches(xaml, $@"xmlns\s*:\s*({Prefix})\s*=\s*([""'])http://schemas\.microsoft\.com/winfx/{year}/xaml\2"),
                    comments
                    );

                if (match != null)
                {
                    return new XamlX(
                        match.Index,
                        match.Length,
                        match.Groups[1].Value
                        );
                }
            }

            if (FirstNotCommented(Regex.Matches(xaml, @"xmlns\s*:\s*x\s*="), comments) != null)
            {
                //`x` is declared, but as something else: there is no xaml language alias
                return new XamlX(0, 0, "NO_X_ALIAS");
            }

            return new XamlX(0, 0, "x");
        }

        /// <summary>
        /// The first match which is not inside a comment, if any.
        /// </summary>
        private static Match? FirstNotCommented(
            MatchCollection matches,
            List<(int Start, int End)> comments
            )
        {
            foreach (Match match in matches)
            {
                if (!IsCommented(comments, match.Index))
                {
                    return match;
                }
            }

            return null;
        }

        /// <summary>
        /// Find every namespace declaration (<c>xmlns:alias="..."</c> and <c>xmlns="..."</c>)
        /// with the element it is written on. The CLR namespace mappings are
        /// <c>clr-namespace:A.B.C</c> and <c>using:A.B.C</c>; the other ones matter only
        /// because they shadow a mapping of the same alias.
        /// </summary>
        private static IEnumerable<XamlXmlns> ReadXmlns(
            string xaml,
            List<(int Start, int End)> comments,
            List<XamlElement> elements
            )
        {
            //the whitespace around the `:` and the `=` of an attribute is allowed by xml
            var matches = Regex.Matches(
                xaml,
                $@"\bxmlns(?:\s*:\s*({Prefix}))?\s*=\s*([""'])(.*?)\2"
                );
            foreach (Match match in matches)
            {
                if (IsCommented(comments, match.Index))
                {
                    continue;
                }

                var scopeStart = 0;
                var scopeEnd = int.MaxValue;
                foreach (var element in elements)
                {
                    if (element.Start <= match.Index && match.Index < element.StartTagEnd)
                    {
                        scopeStart = element.Start;
                        scopeEnd = element.End;
                        break;
                    }
                }

                yield return new XamlXmlns(
                    match.Index,
                    match.Length,
                    match.Groups[1].Value,
                    match.Groups[3].Value,
                    match.Groups[2].Value[0],
                    scopeStart,
                    scopeEnd
                    );
            }
        }

        /// <summary>
        /// Find the opening and closing tags: <c>&lt;alias:ClassName</c>, <c>&lt;/alias:ClassName</c>
        /// and the ones without a prefix (<c>&lt;ClassName</c>), which reference a class when the
        /// default declaration in scope maps a CLR namespace.
        /// </summary>
        private static IEnumerable<XamlControl> ReadControls(
            string xaml,
            List<(int Start, int End)> comments
            )
        {
            var matches = Regex.Matches(xaml, $@"<\s*(/?)\s*(?:({Prefix})\s*:\s*)?({ClassName})");
            foreach (Match match in matches)
            {
                if (IsCommented(comments, match.Index))
                {
                    continue;
                }

                yield return new XamlControl(
                    match.Index,
                    match.Length,
                    match.Groups[1].Value,
                    match.Groups[2].Value,
                    match.Groups[3].Value
                    );
            }
        }

        /// <summary>
        /// Find the <c>x:Class="A.B.ClassName"</c> attributes.
        /// </summary>
        private static IEnumerable<XamlClass> ReadClasses(
            XamlX xPrefix,
            string xaml,
            List<(int Start, int End)> comments
            )
        {
            var x = Regex.Escape(xPrefix.Alias);

            var matches = Regex.Matches(xaml, $@"\b{x}:Class\s*=\s*([""'])([\w.]+)\1");
            foreach (Match match in matches)
            {
                if (IsCommented(comments, match.Index))
                {
                    continue;
                }

                yield return new XamlClass(
                    match.Index,
                    match.Length,
                    match.Groups[2].Value,
                    match.Groups[1].Value[0]
                    );
            }
        }

        /// <summary>
        /// Remove the CLR namespace declarations which became unused by the move. A declaration
        /// which was unused before is the user's business and stays (the <c>xmlns:local</c> of
        /// a project template, for example); the default declaration is never removed.
        /// </summary>
        private static void Cleanup(
            ref string xaml,
            HashSet<(string, string, string)> usedBefore
            )
        {
            var r = ReadStructure(xaml);

            var used = ReadUsedDeclarations(xaml, r);

            //in backward order!
            foreach (var xmlns in r.Xmlns.Where(x => x.IsClr && x.Alias.Length > 0).OrderByDescending(x => x.Index))
            {
                if (used.Contains(xmlns))
                {
                    continue;
                }

                if (!usedBefore.Contains(KeyOf(xmlns)))
                {
                    continue;
                }

                xmlns.Remove(ref xaml);
            }
        }

        /// <summary>
        /// The keys of the declarations which are used somewhere in the body,
        /// see <see cref="ReadUsedDeclarations"/>.
        /// </summary>
        private static HashSet<(string, string, string)> ReadUsedDeclarationKeys(
            string xaml,
            XamlStructure structure
            )
        {
            return new HashSet<(string, string, string)>(
                ReadUsedDeclarations(xaml, structure).Select(KeyOf)
                );
        }

        /// <summary>
        /// A declaration as the same one before and after the move, when its position changes.
        /// </summary>
        private static (string, string, string) KeyOf(XamlXmlns xmlns)
        {
            return (xmlns.Alias, xmlns.Namespace, xmlns.Suffix);
        }

        /// <summary>
        /// Every declaration which is used somewhere in the body.
        ///
        /// An alias may be referenced not only by a tag or by an <c>x:Type</c>/<c>x:Static</c>
        /// markup extension, but also by a custom markup extension (<c>{conv:UpperCase}</c>),
        /// by an attached property (<c>attached:Helper.IsEnabled="True"</c>) and by an Avalonia
        /// selector (<c>local|MyButton</c>), so anything which looks like <c>alias:</c> or
        /// <c>alias|</c> is taken into account here, resolved by its scope. An unrecognized usage
        /// would cost us a removed xmlns clause and a broken document, hence this greediness.
        /// </summary>
        /// <param name="xaml">Body of the document.</param>
        /// <param name="structure">The structure of that body: the declarations themselves are
        /// excluded from the scan, otherwise every alias is used by its own declaration.</param>
        private static HashSet<XamlXmlns> ReadUsedDeclarations(
            string xaml,
            XamlStructure structure
            )
        {
            var body = new StringBuilder(xaml);

            foreach (var xmlns in structure.Xmlns.Where(x => x.Saved))
            {
                for (var i = xmlns.Index; i < Math.Min(xmlns.Index + xmlns.Length, body.Length); i++)
                {
                    body[i] = ' ';
                }
            }

            var result = new HashSet<XamlXmlns>();
            foreach (Match match in Regex.Matches(body.ToString(), $@"({Prefix})\s*[:|]"))
            {
                var declaration = structure.GetByAlias(match.Groups[1].Value, match.Index);
                if (declaration != null)
                {
                    result.Add(declaration);
                }
            }

            return result;
        }

        /// <summary>
        /// An element of the document: the span of its start tag and of the whole element.
        /// </summary>
        private readonly struct XamlElement
        {
            public readonly int Start;

            public readonly int StartTagEnd;

            public readonly int End;

            public XamlElement(int start, int startTagEnd, int end)
            {
                Start = start;
                StartTagEnd = startTagEnd;
                End = end;
            }

            public XamlElement ClosedAt(int end)
            {
                return new XamlElement(Start, StartTagEnd, end);
            }
        }
    }
}
