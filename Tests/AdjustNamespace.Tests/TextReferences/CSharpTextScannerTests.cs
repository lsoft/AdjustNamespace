using AdjustNamespace.TextReferences;
using AdjustNamespace.Tests.Infrastructure;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// Tests of the C# scanner: which parts of a file it hands to the search, and that the
    /// spans of the matches are exact.
    /// </summary>
    public class CSharpTextScannerTests
    {
        private const string Path = @"C:\sln\App\Factory.cs";

        private static System.Collections.Generic.List<TextReferenceMatch> Scan(string source, bool comments = false)
        {
            return TextReferenceScanner.ScanCSharpText(TextReferenceCorpus.Set(), Path, source, comments);
        }

        private static void AssertSpanIsTheName(string source, TextReferenceMatch match)
        {
            Assert.Equal(match.MatchedText, source.Substring(match.Span.Start, match.Span.Length));
        }

        [Theory]
        [InlineData("class C { string s = \"Legacy.Models.Customer\"; }")]
        [InlineData("class C { string s = @\"Legacy.Models.Customer\"; }")]
        [InlineData("class C { string s = \"\"\"Legacy.Models.Customer\"\"\"; }")]
        [InlineData("class C { string s = \"\"\"\n    Legacy.Models.Customer\n    \"\"\"; }")]
        [InlineData("class C { object s = \"Legacy.Models.Customer\"u8; }")]
        [InlineData("class C { string s = $\"{x}Legacy.Models.Customer\"; }")]
        [InlineData("class C { string s = $@\"{x}Legacy.Models.Customer\"; }")]
        [InlineData("class C { string s = $\"Legacy.Models.Customer{x}\"; }")]
        [InlineData("class C { string s = $\"\"\"Legacy.Models.Customer{x}\"\"\"; }")]
        public void The_span_of_a_match_is_the_name_whatever_the_kind_of_the_literal_is(string source)
        {
            var match = Assert.Single(Scan(source));

            AssertSpanIsTheName(source, match);
            Assert.Equal("Legacy.Models.Customer", match.MatchedText);
            Assert.Equal("Lib.Models.Customer", match.ReplacementText);
        }

        [Fact]
        public void A_literal_with_escapes_before_the_name_still_has_an_exact_span()
        {
            var source = "class C { string s = \"tab\\there: Legacy.Models.Customer\"; string t = \"\\u0041 Legacy.Models.Order\"; }";

            var matches = Scan(source);

            Assert.Equal(2, matches.Count);
            Assert.All(matches, m => AssertSpanIsTheName(source, m));
        }

        [Fact]
        public void Several_names_in_one_literal_are_all_found()
        {
            var source = "class C { string s = \"Legacy.Models.Customer;Legacy.Models.Order\"; }";

            var matches = Scan(source);

            Assert.Equal(new[] { "Legacy.Models.Customer", "Legacy.Models.Order" }, matches.Select(m => m.MatchedText).ToArray());
            Assert.All(matches, m => AssertSpanIsTheName(source, m));
        }

        [Fact]
        public void The_line_and_the_column_are_those_of_the_name()
        {
            var source = "class C\r\n{\r\n    string s = \"Legacy.Models.Customer\";\r\n}\r\n";

            var match = Assert.Single(Scan(source));

            Assert.Equal(3, match.Line);
            Assert.Equal("    string s = \"".Length, match.Column);
            Assert.Equal("    string s = \"Legacy.Models.Customer\";", match.LineText);
        }

        [Fact]
        public void A_name_in_an_interpolation_hole_is_code_and_not_text()
        {
            var matches = Scan("class C { string s = $\"{Legacy.Models.Customer.Name}\"; }");

            Assert.Empty(matches);
        }

        [Fact]
        public void The_matches_are_in_the_order_of_the_positions_once_numbered()
        {
            var source = "class C { string b = \"Legacy.Models.Order\"; string a = \"Legacy.Models.Customer\"; }";

            var result = TextReferenceScanner.Finish(Scan(source), TextReferenceOptions.Default);

            Assert.Equal(new[] { 1, 2 }, result.Matches.Select(m => m.Id).ToArray());
            Assert.Equal("Legacy.Models.Order", result.Matches[0].MatchedText);
        }

        /// <summary>
        /// A parameter named <c>typeName</c> says the string is a type name even though the
        /// method is the user's own: only the semantic model knows that.
        /// </summary>
        [Fact]
        public async Task A_parameter_named_type_name_makes_the_string_a_type_name_through_the_semantic_model()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Customer.cs", "namespace Legacy.Models { public class Customer { } }")
                .AddDocument("MyApp", "Factory.cs",
@"namespace Other
{
    public static class Factory
    {
        public static void Register(string typeName) { }

        public static void Describe(string message) { }

        public static void Run()
        {
            Register(""Legacy.Models.Customer"");
            Describe(""Legacy.Models.Customer"");
            Register(typeName: ""Legacy.Models.Customer"");
        }
    }
}
")
                ;

            var result = await ScanAsync(solution);

            var matches = result.Matches.Where(m => m.FilePath == solution.PathOf("MyApp", "Factory.cs")).ToList();

            Assert.Equal(3, matches.Count);
            Assert.Equal(2, matches.Count(m => m.Place == TextReferencePlace.KnownTypeNameArgument));
            Assert.Equal(1, matches.Count(m => m.Place == TextReferencePlace.CSharpStringLiteral));
        }

        [Fact]
        public async Task The_semantic_model_tells_the_framework_methods_without_looking_at_their_names()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Customer.cs", "namespace Legacy.Models { public class Customer { } }")
                .AddDocument("MyApp", "Factory.cs",
@"using System;

namespace Other
{
    public static class Factory
    {
        public static Type Run(System.Reflection.Assembly assembly)
        {
            //Assembly.GetType takes a parameter named `name`, so the method list is what tells it
            var a = assembly.GetType(""Legacy.Models.Customer"");

            return Type.GetType(""Legacy.Models.Customer, MyApp"");
        }
    }
}
")
                ;

            var result = await ScanAsync(solution);

            var matches = result.Matches.Where(m => m.FilePath == solution.PathOf("MyApp", "Factory.cs")).ToList();

            Assert.Equal(2, matches.Count);
            Assert.All(matches, m => Assert.Equal(ConfidenceLevel.Certain, m.Level));
        }

        [Fact]
        public async Task A_file_that_names_nothing_is_not_walked_and_generated_files_are_skipped()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Customer.cs", "namespace Legacy.Models { public class Customer { } }")
                .AddDocument("MyApp", "Quiet.cs", "namespace Other { public class Quiet { string s = \"nothing\"; } }")
                .AddDocument("MyApp", "Form.Designer.cs", "namespace Other { public class Form { string s = \"Legacy.Models.Customer\"; } }")
                ;

            var result = await ScanAsync(solution);

            Assert.All(result.Matches, m => Assert.Equal(solution.PathOf("MyApp", @"Models\Customer.cs"), m.FilePath));
        }

        private static async System.Threading.Tasks.Task<TextReferenceScanResult> ScanAsync(TestSolution solution)
        {
            var path = solution.PathOf("MyApp", @"Models\Customer.cs");

            return await TextReferenceScanner.ScanAsync(
                solution.Context,
                new AdjustNamespace.NamespaceReplaceRegex(string.Empty, string.Empty),
                new[] { path },
                TextReferenceOptions.Default
                );
        }
    }
}
