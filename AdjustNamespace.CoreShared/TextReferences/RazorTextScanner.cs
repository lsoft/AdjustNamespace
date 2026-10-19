using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Finds the parts of a Razor file (<c>.cshtml</c>, <c>.razor</c>, <c>_ViewImports</c>) that
    /// name a type or a namespace: the directives <c>@using</c>, <c>@model</c>,
    /// <c>@inherits</c>, <c>@inject</c>, <c>@typeparam</c>, <c>@implements</c> and
    /// <c>@namespace</c>. A directive names a type for the compiler and not for Roslyn (the
    /// view is compiled out of the file), so nothing but a text search follows the move.
    ///
    /// The generic arguments of a directive (<c>@model List&lt;A.B.C&gt;</c>,
    /// <c>@inject IRepo&lt;A.B.C&gt; repo</c>) are in the rest of the line, which is searched as a
    /// whole.
    /// </summary>
    internal static class RazorTextScanner
    {
        private static readonly Regex Directive = new(
            @"^[ \t]*@(?<name>using|model|inherits|inject|typeparam|namespace|implements)(?=[ \t])(?<rest>[^\r\n]*)",
            RegexOptions.Multiline | RegexOptions.CultureInvariant
            );

        public static List<TextCandidate> Scan(string text)
        {
            var result = new List<TextCandidate>();

            foreach (Match match in Directive.Matches(text))
            {
                var rest = match.Groups["rest"];

                var namesNamespace = match.Groups["name"].Value == "using"
                    || match.Groups["name"].Value == "namespace";

                result.Add(
                    new TextCandidate(
                        TextReferencePlace.RazorDirective,
                        rest.Index,
                        rest.Length,
                        expectsNamespace: namesNamespace,
                        //`@using static A.B.C` and `@using Alias = A.B.C` name a type
                        alsoSearchTypes: true
                        )
                    );
            }

            return result;
        }
    }
}
