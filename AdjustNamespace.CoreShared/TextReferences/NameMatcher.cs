using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Finds the full names of the moved types in a text.
    ///
    /// Only the full names are searched, never a short name (<c>Customer</c>, <c>Models</c> are
    /// far too common). A name has borders: <c>X.A.B.C</c> (a dot or an identifier character in
    /// front) and <c>A.B.CExtra</c> (an identifier character behind) do not match, a dot behind
    /// is allowed (<c>A.B.C.Member</c>). The forms that are recognized besides the plain name:
    /// <c>global::A.B.C</c>, the metadata forms <c>A.B.Outer+Inner</c> and <c>A.B.C`1</c> and the
    /// assembly qualified <c>A.B.C, MyAsm, Version=...</c>.
    /// </summary>
    internal sealed class NameMatcher
    {
        /// <summary>
        /// A dotted identifier chain that does not continue to the left.
        /// </summary>
        private static readonly Regex DottedName = new(
            @"(?<![\w.])[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+",
            RegexOptions.CultureInvariant
            );

        /// <summary>
        /// <c>, MyAsm</c> with the optional <c>, Version=...</c> parts.
        /// </summary>
        private static readonly Regex AssemblyPart = new(
            @"\G\s*,\s*(?<asm>[A-Za-z_][\w.\-]*)(?<rest>(?:\s*,\s*(?:Version|Culture|PublicKeyToken|ProcessorArchitecture|Retargetable|ContentType)\s*=\s*[^,\]""'<>;\r\n]*)*)",
            RegexOptions.CultureInvariant
            );

        /// <summary>
        /// <c>Alias =</c> in front of a namespace (<c>@using Alias = A.B</c>).
        /// </summary>
        private static readonly Regex AliasPrefix = new(
            @"\G[A-Za-z_]\w*\s*=\s*",
            RegexOptions.CultureInvariant
            );

        private static readonly Regex Identifier = new(
            @"\G[A-Za-z_]\w*",
            RegexOptions.CultureInvariant
            );

        private const string GlobalPrefix = "global::";

        private readonly MovedTypeSet _set;

        public NameMatcher(
            MovedTypeSet set
            )
        {
            if (set is null)
            {
                throw new ArgumentNullException(nameof(set));
            }

            _set = set;
        }

        /// <summary>
        /// The names of the moved types written in the given part of the text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="start">Where to start searching; the text in front of it is not looked at.</param>
        /// <param name="length">How much of the text to search.</param>
        public List<NameOccurrence> FindTypes(
            string text,
            int start,
            int length
            )
        {
            var result = new List<NameOccurrence>();
            if (length <= 0 || _set.IsEmpty)
            {
                return result;
            }

            var end = start + length;

            var match = DottedName.Match(text, start, length);
            while (match.Success)
            {
                var occurrence = TryRecognize(text, match, start, end);
                if (occurrence != null)
                {
                    result.Add(occurrence);
                }

                match = match.NextMatch();
            }

            return result;
        }

        /// <summary>
        /// The text of the given part names a namespace that is moved as a whole, and nothing
        /// else: <c>A.B</c>, <c>A.B;</c>, <c>global::A.B</c>.
        /// </summary>
        /// <returns>The namespace and where its name is, or <c>null</c>.</returns>
        public (MovedNamespace Namespace, int Start, int Length)? FindWholeNamespace(
            string text,
            int start,
            int length
            )
        {
            var from = start;
            var to = start + length;

            while (from < to && char.IsWhiteSpace(text[from]))
            {
                from++;
            }

            while (to > from && (char.IsWhiteSpace(text[to - 1]) || text[to - 1] == ';'))
            {
                to--;
            }

            //`Alias = A.B`
            var alias = AliasPrefix.Match(text, from, to - from);
            if (alias.Success)
            {
                from += alias.Length;
            }

            if (string.CompareOrdinal(text, from, GlobalPrefix, 0, GlobalPrefix.Length) == 0
                && from + GlobalPrefix.Length <= to)
            {
                from += GlobalPrefix.Length;
            }

            if (to <= from)
            {
                return null;
            }

            var name = text.Substring(from, to - from);

            var @namespace = _set.NamespaceOf(name);
            if (@namespace == null)
            {
                return null;
            }

            return (@namespace, from, to - from);
        }

        private NameOccurrence? TryRecognize(
            string text,
            Match match,
            int regionStart,
            int regionEnd
            )
        {
            var token = match.Value;

            //the longest prefix of the chain that is the full name of a moved type
            var dotIndex = token.Length;
            MovedType[]? candidates = null;
            var nameLength = 0;

            while (dotIndex > 0)
            {
                var prefix = token.Substring(0, dotIndex);

                if (prefix.IndexOf('.') >= 0 && _set.IsKnownFullName(prefix))
                {
                    candidates = new List<MovedType>(_set.TypesOf(prefix)).ToArray();
                    nameLength = dotIndex;
                    break;
                }

                dotIndex = token.LastIndexOf('.', dotIndex - 1);
                if (dotIndex < 0)
                {
                    break;
                }
            }

            if (candidates == null)
            {
                return null;
            }

            var nameStart = match.Index;
            var nameEnd = nameStart + nameLength;
            var remainder = token.Substring(nameLength);

            var writtenArity = -1;
            var nestedPath = string.Empty;
            string? assembly = null;

            //the occurrence ends where the dotted chain ends (A.B.C.Member), or where the
            //metadata form and the assembly behind the name end
            var fullEnd = nameEnd + remainder.Length;

            if (remainder.Length == 0)
            {
                ParseMetadataForm(text, regionEnd, ref fullEnd, out writtenArity, out nestedPath);
                assembly = ParseAssembly(text, regionEnd, ref fullEnd);
            }

            var type = ChooseByArity(candidates, writtenArity);

            var fullStart = nameStart;
            if (nameStart - GlobalPrefix.Length >= regionStart
                && string.CompareOrdinal(text, nameStart - GlobalPrefix.Length, GlobalPrefix, 0, GlobalPrefix.Length) == 0)
            {
                fullStart = nameStart - GlobalPrefix.Length;
            }

            return new NameOccurrence(
                type,
                nameStart,
                nameLength,
                fullStart,
                fullEnd,
                writtenArity,
                nestedPath,
                assembly,
                remainder
                );
        }

        /// <summary>
        /// The type of the arity written behind the name; a name without an arity means the
        /// type that is not generic, if there is one.
        /// </summary>
        private static MovedType ChooseByArity(MovedType[] candidates, int writtenArity)
        {
            foreach (var candidate in candidates)
            {
                if (writtenArity >= 0 && candidate.Arity == writtenArity)
                {
                    return candidate;
                }
            }

            foreach (var candidate in candidates)
            {
                if (candidate.Arity == 0)
                {
                    return candidate;
                }
            }

            return candidates[0];
        }

        /// <summary>
        /// <c>`1</c> and <c>+Inner</c> behind the name.
        /// </summary>
        private static void ParseMetadataForm(
            string text,
            int regionEnd,
            ref int position,
            out int arity,
            out string nestedPath
            )
        {
            arity = -1;
            var nested = new List<string>();

            while (position < regionEnd)
            {
                if (text[position] == '`')
                {
                    var digitsEnd = position + 1;
                    while (digitsEnd < regionEnd && char.IsDigit(text[digitsEnd]))
                    {
                        digitsEnd++;
                    }

                    if (digitsEnd == position + 1)
                    {
                        break;
                    }

                    arity = int.Parse(text.Substring(position + 1, digitsEnd - position - 1));
                    position = digitsEnd;
                    continue;
                }

                if (text[position] == '+')
                {
                    var identifier = Identifier.Match(text, position + 1, regionEnd - position - 1);
                    if (!identifier.Success)
                    {
                        break;
                    }

                    nested.Add(identifier.Value);
                    position += 1 + identifier.Length;
                    continue;
                }

                break;
            }

            nestedPath = string.Join("+", nested);
        }

        /// <summary>
        /// <c>, MyAsm, Version=1.0.0.0, Culture=neutral</c> behind the name. A comma in prose
        /// (<c>A.B.C, which is ...</c>) is not an assembly.
        /// </summary>
        private static string? ParseAssembly(
            string text,
            int regionEnd,
            ref int position
            )
        {
            if (position >= regionEnd)
            {
                return null;
            }

            var match = AssemblyPart.Match(text, position, regionEnd - position);
            if (!match.Success)
            {
                return null;
            }

            var end = match.Index + match.Length;

            if (match.Groups["rest"].Length == 0 && !IsEndOfAssemblyName(text, end, regionEnd))
            {
                return null;
            }

            position = end;

            return match.Groups["asm"].Value;
        }

        /// <summary>
        /// What follows a bare assembly name: the end of the text or a delimiter, but not
        /// another word.
        /// </summary>
        private static bool IsEndOfAssemblyName(string text, int position, int regionEnd)
        {
            if (position >= regionEnd)
            {
                return true;
            }

            var next = text[position];

            if (char.IsWhiteSpace(next))
            {
                var following = position;
                while (following < regionEnd && char.IsWhiteSpace(text[following]))
                {
                    following++;
                }

                return following >= regionEnd;
            }

            return "])\"';<>,}|&".IndexOf(next) >= 0;
        }
    }
}
