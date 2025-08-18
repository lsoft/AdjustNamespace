using System.Text.RegularExpressions;

namespace AdjustNamespace
{
    public sealed class NamespaceReplaceRegex
    {
        public string ReplaceRegex
        {
            get;
        }

        public string ReplacedString
        {
            get;
        }

        public NamespaceReplaceRegex(string replaceRegex, string replacedString)
        {
            ReplaceRegex = replaceRegex;
            ReplacedString = replacedString;
        }

        public string Modify(string myNamespace)
        {
            if (string.IsNullOrEmpty(ReplaceRegex))
            {
                return myNamespace;
            }
            if (string.IsNullOrEmpty(ReplacedString))
            {
                return myNamespace;
            }

            var result = Regex.Replace(myNamespace, ReplaceRegex, ReplacedString);
            return result;
        }
    }
}
