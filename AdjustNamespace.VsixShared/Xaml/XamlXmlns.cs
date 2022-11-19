using System;
using System.Linq;

namespace AdjustNamespace.Xaml
{
    public class XamlXmlns : IXamlPositioned
    {
        public int Index
        {
            get;
        }
        public int Length
        {
            get;
        }
        public string Alias
        {
            get;
        }
        public string Namespace
        {
            get;
        }
        public bool Saved
        {
            get;
        }
        public string Suffix
        {
            get;
        }

        public XamlXmlns(int index, int length, string alias, string @namespace, string suffix)
        {
            Index = index;
            Length = length;
            Alias = alias;
            Namespace = @namespace;
            Saved = true;
            Suffix = suffix;
        }

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
        }

        internal void SaveTo(ref string xaml, ref int indexToInsert)
        {
            var s = $@" xmlns:{Alias}=""clr-namespace:{Namespace}{Suffix}""";
            xaml = xaml.Insert(indexToInsert, s);
            indexToInsert += s.Length;
        }

        internal void Remove(ref string xaml)
        {
            xaml = xaml.Substring(0, Index) + xaml.Substring(Index + Length);
        }

        private string GetLastWord(string s)
        {
            if (s.Contains('.'))
            {
                return s.Substring(s.LastIndexOf('.') + 1);
            }

            return s;
        }

        private string GetPartOfGuid()
        {
            var g = Guid.NewGuid().ToString();
            g = g.Substring(0, g.IndexOf('-'));

            return g;
        }

    }
}
