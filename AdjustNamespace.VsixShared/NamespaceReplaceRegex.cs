using System.Text.RegularExpressions;

namespace AdjustNamespace
{
    /// <summary>
    /// User defined regex which additionally modifies the target namespace
    /// determined from the file location.
    /// For example, the regex `^[^.]+` with the replacement `NewRoot` renames
    /// the first part of every target namespace.
    /// The user enters it on the second step of the wizard; see also
    /// <see cref="UI.ViewModel.Select.KnownRegex"/> for the built in samples.
    /// </summary>
    public sealed class NamespaceReplaceRegex
    {
        /// <summary>
        /// The regex to search for. An empty string disables the modification.
        /// </summary>
        public string ReplaceRegex
        {
            get;
        }

        /// <summary>
        /// The replacement for the found fragment. An empty string disables the modification.
        /// </summary>
        public string ReplacedString
        {
            get;
        }

        public NamespaceReplaceRegex(string replaceRegex, string replacedString)
        {
            ReplaceRegex = replaceRegex;
            ReplacedString = replacedString;
        }

        /// <summary>
        /// Apply the regex to the given namespace.
        /// </summary>
        /// <returns>The modified namespace, or the original one if the modification is disabled.</returns>
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
