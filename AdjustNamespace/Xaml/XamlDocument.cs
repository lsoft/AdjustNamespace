using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml
{
    public class XamlDocument : IXmlnsProvider
    {
        private readonly string _xamlFilePath;
        private string _xaml;

        private bool _changesExists = false;

        public XamlX XPrefix
        {
            get; private set;
        } = null!;
        public List<XamlXmlns> Xmlns
        {
            get; private set;
        } = null!;
        public List<XamlControl> Controls
        {
            get; private set;
        } = null!;
        public List<XamlAttributeReference> RefFroms
        {
            get; private set;
        } = null!;
        public List<XamlClass> Classes
        {
            get; private set;
        } = null!;

        public XamlDocument(
            string xamlFilePath
            )
        {
            if (xamlFilePath == null)
                throw new ArgumentNullException(nameof(xamlFilePath));

            _xamlFilePath = xamlFilePath;

            _xaml = File.ReadAllText(xamlFilePath);

            Reload();
        }

        private IEnumerable<XamlAttributeReference> ReadRefFromAttributes()
        {
            var matches0 = Regex.Matches(_xaml, @$"{{\s?{XPrefix.Alias}:Type\s+([\w\d]+)\s?:\s?([\w\d]+)");
            foreach (Match match in matches0)
            {
                var xar = new XamlAttributeReference(
                    this,
                    match.Index,
                    match.Length,
                    "Type",
                    match.Groups[1].Value,
                    match.Groups[2].Value
                    );

                yield return xar;
            }
            var matches1 = Regex.Matches(_xaml, @$"{{\s?{XPrefix.Alias}:Static\s+([\w\d]+)\s?:\s?([\w\d]+)");
            foreach (Match match in matches1)
            {
                var xar = new XamlAttributeReference(
                    this,
                    match.Index,
                    match.Length,
                    "Static",
                    match.Groups[1].Value,
                    match.Groups[2].Value
                    );

                yield return xar;
            }
        }

        private XamlX ReadXPrefix()
        {
            var matches = Regex.Matches(_xaml, @"xmlns\s?:\s?([\w\d]+)\s?=\s?\""http:\/\/schemas\.microsoft\.com\/winfx\/2006\/xaml\""");
            var match = matches[0];

            return new XamlX(
                match.Index,
                match.Length,
                match.Groups[1].Value
                );
        }

        private IEnumerable<XamlXmlns> ReadXmlns()
        {
            var matches = Regex.Matches(_xaml, @"xmlns:([\w\d]+)=""clr-namespace:([\w\d._]+)([^""]*)""");
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

        private IEnumerable<XamlControl> ReadControls()
        {
            var matches = Regex.Matches(_xaml, @"<\s?(\/?)\s?([\w\d]+)\s?:\s?([\w\d]+)");
            foreach (Match match in matches)
            {
                var xc = new XamlControl(
                    this,
                    match.Index,
                    match.Length,
                    match.Groups[1].Value,
                    match.Groups[2].Value,
                    match.Groups[3].Value
                    );

                yield return xc;
            }
        }

        private IEnumerable<XamlClass> ReadClasses()
        {
            var matches = Regex.Matches(_xaml, @"x:Class\s?=\s?""([\w\d._]+)""");
            foreach (Match match in matches)
            {
                var xc = new XamlClass(
                    this,
                    match.Index,
                    match.Length,
                    match.Groups[1].Value
                    );

                yield return xc;
            }
        }

        public XamlXmlns GetByAlias(string alias) => Xmlns.First(x => x.Alias == alias);

        public XamlXmlns? TryGetByNamespace(string @namespace) => Xmlns.FirstOrDefault(x => x.Namespace == @namespace);

        public XamlX GetXPrefix() => XPrefix;

        public void Reload()
        {
            XPrefix = ReadXPrefix();
            Xmlns = ReadXmlns().ToList();
            Controls = ReadControls().ToList();
            RefFroms = ReadRefFromAttributes().ToList();
            Classes = ReadClasses().ToList();
        }

        public void Cleanup()
        {
            var aliases = new HashSet<string>();
            Controls.ForEach(c => aliases.Add(c.Alias));
            RefFroms.ForEach(c => aliases.Add(c.Alias));

            foreach (var xmlns in Xmlns.OrderByDescending(x => x.Index))
            {
                if (aliases.Contains(xmlns.Alias))
                {
                    continue;
                }

                xmlns.Remove(ref _xaml);
            }
        }

        public void MoveObject(
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

            var combined = new List<IXamlPerformable>();
            combined.AddRange(Controls);
            combined.AddRange(RefFroms);
            combined.AddRange(Classes);

            //in backward order!
            foreach (var positioned in combined.OrderByDescending(c => c.Index))
            {
                var performable = positioned as IXamlPerformable;

                if (performable.Perform(
                    sourceNamespace,
                    objectClassName,
                    targetNamespace,
                    ref _xaml,
                    out var newXmlns
                    ))
                {
                    _changesExists = true;

                    if (newXmlns != null)
                    {
                        Xmlns.Add(newXmlns);
                    }
                }
            }

            var reloadedXmlns = ReadXmlns().ToList();

            var indexToInsert = reloadedXmlns.Max(x => x.Index + x.Length);

            foreach (var xmlns in Xmlns.Where(x => !x.Saved))
            {
                xmlns.SaveTo(ref _xaml, ref indexToInsert);
            }

        }

        internal void SaveIfChangesExists()
        {
            if (!_changesExists)
            {
                return;
            }
            
            File.WriteAllText(_xamlFilePath, _xaml);
        }

        internal bool GetRootInfo(out string? rootNamespace, out string? rootName)
        {
            if (Classes.Count == 0)
            {
                rootNamespace = null;
                rootName = null;
                return false;
            }

            rootNamespace = Classes[0].Namespace;
            rootName = Classes[0].ClassName;
            return true;
        }
    }
}
