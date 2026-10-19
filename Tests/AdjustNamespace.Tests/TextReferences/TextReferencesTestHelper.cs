using AdjustNamespace.TextReferences;
using System.Collections.Generic;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// Two matches to build a report out of.
    /// </summary>
    internal static class TextReferencesTestHelper
    {
        public static List<TextReferenceMatch> Matches(MovedTypeSet set)
        {
            var matches = TextReferenceScanner.ScanCSharpText(
                set,
                @"C:\sln\App\Factory.cs",
                "class C {\n    string a = \"Legacy.Models.Customer\";\n    string b = \"Legacy.Models.Order\";\n}",
                false
                );

            var result = TextReferenceScanner.Finish(matches, TextReferenceOptions.Default);

            return new List<TextReferenceMatch>(result.Matches);
        }
    }
}
