using AdjustNamespace.TextReferences;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// The corpus of the score: real looking examples of a name written as a text, each with the
    /// level it has to land in. A weight of <see cref="ScoreSignals"/> is a starting point, and
    /// changing one is checked against every example here, so a positive that becomes a negative
    /// (or the other way round) is seen at once.
    ///
    /// The C# examples are scored without a semantic model, i.e. a string is told to be a type
    /// name by the name of the method it is passed to; the semantic route is covered by the
    /// scanner tests.
    /// </summary>
    public class TextReferenceScoringCorpusTests
    {
        private const string Cs = @"C:\sln\App\Factory.cs";

        private static TextReferenceMatch Single(string path, string source, bool comments = false)
        {
            var matches = TextReferenceScanner.ScanCSharpText(TextReferenceCorpus.Set(), path, source, comments);

            return Assert.Single(matches);
        }

        private static TextReferenceMatch InCode(string statement, bool comments = false)
        {
            return Single(Cs, "class C { void M() { " + statement + " } }", comments);
        }

        // ---- the positives: a string that is passed as a type name ----

        [Theory]
        [InlineData(@"var t = Type.GetType(""Legacy.Models.Customer, Lib"");", ConfidenceLevel.Certain)]
        [InlineData(@"var t = Type.GetType(""Legacy.Models.Customer"");", ConfidenceLevel.Certain)]
        [InlineData(@"var t = Type.GetType(""Legacy.Models.Customer, Lib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"", true);", ConfidenceLevel.Certain)]
        [InlineData(@"var t = asm.GetType(""Legacy.Models.Customer"");", ConfidenceLevel.Certain)]
        [InlineData(@"var o = Activator.CreateInstance(""Lib"", ""Legacy.Models.Customer"");", ConfidenceLevel.Certain)]
        [InlineData(@"var o = domain.CreateInstanceAndUnwrap(""Lib"", ""Legacy.Models.Customer"");", ConfidenceLevel.Certain)]
        [InlineData(@"var m = new ResourceManager(""Legacy.Models.Order"", asm);", ConfidenceLevel.Certain)]
        [InlineData(@"var t = Type.GetType(""Legacy.Models.Order+Item, Lib"");", ConfidenceLevel.Certain)]
        [InlineData(@"var t = Type.GetType(""Legacy.Models.Repo`1, Lib"");", ConfidenceLevel.Certain)]
        public void A_string_passed_as_a_type_name_is_certain(string statement, ConfidenceLevel expected)
        {
            var match = InCode(statement);

            Assert.Equal(expected, match.Level);
            Assert.Equal(TextReferencePlace.KnownTypeNameArgument, match.Place);
        }

        [Theory]
        [InlineData(@"[TypeConverter(""Legacy.Models.Customer, Lib"")] class D { }")]
        [InlineData(@"[Designer(""Legacy.Models.Customer"")] class D { }")]
        [InlineData(@"[Editor(""Legacy.Models.Customer, Lib"", ""Legacy.Models.Order"")] class D { }")]
        [InlineData(@"[DebuggerTypeProxy(""Legacy.Models.Order"")] class D { }")]
        public void A_string_passed_to_an_attribute_that_takes_a_type_name_is_certain(string declaration)
        {
            var matches = TextReferenceScanner.ScanCSharpText(TextReferenceCorpus.Set(), Cs, declaration, false);

            Assert.NotEmpty(matches);
            Assert.All(matches, m => Assert.Equal(ConfidenceLevel.Certain, m.Level));
            Assert.All(matches, m => Assert.Equal(TextReferencePlace.KnownTypeNameArgument, m.Place));
        }

        [Fact]
        public void The_score_names_the_reasons()
        {
            var match = InCode(@"var t = Type.GetType(""Legacy.Models.Customer, Lib"");");

            Assert.Equal(100, match.Score.Value);
            Assert.Equal(
                new[]
                {
                    "string passed as a type name",
                    "assembly of the moved type",
                    "exactly the name"
                },
                match.Score.Signals.Select(s => s.Name).ToArray()
                );
            Assert.Equal(new[] { 95, 20, 10 }, match.Score.Signals.Select(s => s.Weight).ToArray());
            Assert.Contains("string passed as a type name +95", match.Score.Explain());
        }

        // ---- any other string literal ----

        [Theory]
        [InlineData(@"const string N = ""Legacy.Models.Customer, Lib"";", ConfidenceLevel.Certain, 90)]
        [InlineData(@"const string N = ""Legacy.Models.Customer"";", ConfidenceLevel.Likely, 70)]
        [InlineData(@"const string N = ""Legacy.Models.Order+Item, Lib"";", ConfidenceLevel.Certain, 100)]
        [InlineData(@"const string N = ""Legacy.Models.Repo`1"";", ConfidenceLevel.Likely, 80)]
        [InlineData(@"const string N = ""Legacy.Models.Customer.Name"";", ConfidenceLevel.Likely, 70)]
        [InlineData(@"const string N = ""global::Legacy.Models.Customer"";", ConfidenceLevel.Likely, 70)]
        [InlineData(@"const string N = @""Legacy.Models.Customer"";", ConfidenceLevel.Likely, 70)]
        [InlineData(@"const string N = """"""Legacy.Models.Customer"""""";", ConfidenceLevel.Likely, 70)]
        [InlineData(@"var n = $""{prefix}Legacy.Models.Customer"";", ConfidenceLevel.Likely, 70)]
        public void A_string_that_is_the_name_is_likely(string statement, ConfidenceLevel expected, int score)
        {
            var match = InCode(statement);

            Assert.Equal(expected, match.Level);
            Assert.Equal(score, match.Score.Value);
            Assert.Equal(TextReferencePlace.CSharpStringLiteral, match.Place);
        }

        [Fact]
        public void A_utf8_string_is_a_string()
        {
            var match = InCode(@"var n = ""Legacy.Models.Customer""u8;");

            Assert.Equal(ConfidenceLevel.Likely, match.Level);
            Assert.Equal("Legacy.Models.Customer", match.MatchedText);
        }

        // ---- the negatives ----

        [Theory]
        [InlineData(@"var s = ""see Legacy.Models.Customer for details"";")]
        [InlineData(@"var s = ""Legacy.Models.Customer is not found"";")]
        [InlineData(@"var s = ""Cannot create an instance of Legacy.Models.Order."";")]
        public void A_name_inside_a_sentence_is_unlikely(string statement)
        {
            var match = InCode(statement);

            Assert.Equal(ConfidenceLevel.Unlikely, match.Level);
            Assert.Contains(match.Score.Signals, s => s.Name == "stands in prose");
        }

        [Theory]
        [InlineData(@"var s = ""Legacy.Models.Customer.resources"";")]
        [InlineData(@"var s = ""Legacy.Models.Customer.cs"";")]
        [InlineData(@"var s = @""C:\data\Legacy.Models.Customer.xml"";")]
        [InlineData(@"var s = ""http://host/Legacy.Models.Customer"";")]
        [InlineData(@"var s = ""Legacy.Models.Customer/edit"";")]
        public void A_name_that_looks_like_a_file_or_a_url_is_unlikely(string statement)
        {
            var match = InCode(statement);

            Assert.Equal(ConfidenceLevel.Unlikely, match.Level);
            Assert.Contains(match.Score.Signals, s => s.Name == "looks like a file or a resource");
        }

        [Fact]
        public void An_assembly_that_is_another_one_makes_it_unlikely()
        {
            var match = InCode(@"const string N = ""Legacy.Models.Customer, OtherLib"";");

            Assert.Equal(ConfidenceLevel.Unlikely, match.Level);
            Assert.Contains(match.Score.Signals, s => s.Name == "assembly of another type" && s.Weight == -40);
        }

        [Fact]
        public void A_type_with_a_namesake_elsewhere_is_never_certain()
        {
            var match = InCode(@"var t = Type.GetType(""Legacy.Util.Clock"");");

            Assert.Equal(ConfidenceLevel.Likely, match.Level);
            Assert.Equal(75, match.Score.Value);
        }

        [Fact]
        public void The_data_of_the_tests_is_worth_less()
        {
            var match = Single(
                @"C:\sln\App.Tests\TestData\Factory.cs",
                @"class C { void M() { var t = Type.GetType(""Legacy.Models.Customer""); } }"
                );

            Assert.Equal(ConfidenceLevel.Likely, match.Level);
            Assert.Equal(85, match.Score.Value);

            var fixtures = Single(
                @"C:\sln\App.Tests\Fixtures\Factory.cs",
                @"class C { void M() { const string N = ""Legacy.Models.Customer""; } }"
                );

            Assert.Equal(50, fixtures.Score.Value);
            Assert.Equal(ConfidenceLevel.Possible, fixtures.Level);
        }

        [Theory]
        [InlineData(@"var s = ""Legacy.Models.CustomerExtra"";")]
        [InlineData(@"var s = ""Other.Legacy.Models.Customer"";")]
        [InlineData(@"var s = ""Customer"";")]
        [InlineData(@"var s = ""Models.Customer"";")]
        [InlineData(@"var s = ""Legacy.Models"";")]
        [InlineData(@"var s = ""Legacy.Alive.Thing"";")]
        public void What_is_not_the_full_name_of_a_moved_type_is_no_match(string statement)
        {
            var matches = TextReferenceScanner.ScanCSharpText(
                TextReferenceCorpus.Set(),
                Cs,
                "class C { void M() { " + statement + " } }",
                false
                );

            Assert.Empty(matches);
        }

        [Fact]
        public void The_code_is_not_a_text()
        {
            var matches = TextReferenceScanner.ScanCSharpText(
                TextReferenceCorpus.Set(),
                Cs,
                "class C { Legacy.Models.Customer F; object M() => typeof(Legacy.Models.Customer); string N = nameof(Legacy.Models.Customer); }",
                true
                );

            Assert.Empty(matches);
        }

        // ---- comments ----

        [Fact]
        public void Comments_are_not_searched_unless_asked_for()
        {
            var matches = TextReferenceScanner.ScanCSharpText(
                TextReferenceCorpus.Set(),
                Cs,
                "class C { /* Legacy.Models.Customer */ // Legacy.Models.Customer\n }",
                false
                );

            Assert.Empty(matches);
        }

        [Theory]
        [InlineData("// see Legacy.Models.Customer")]
        [InlineData("// Legacy.Models.Customer")]
        [InlineData("/* creates Legacy.Models.Order */")]
        public void A_comment_is_unlikely(string comment)
        {
            var match = Single(Cs, "class C { " + comment + "\n }", comments: true);

            Assert.Equal(ConfidenceLevel.Unlikely, match.Level);
            Assert.Equal(TextReferencePlace.Comment, match.Place);
        }

        [Fact]
        public void The_text_of_a_documentation_comment_is_searched_but_a_cref_is_not()
        {
            var matches = TextReferenceScanner.ScanCSharpText(
                TextReferenceCorpus.Set(),
                Cs,
                "class C {\n"
                + "    /// <summary>Legacy.Models.Customer</summary>\n"
                + "    /// <remarks>See <see cref=\"Legacy.Models.Order\"/> and Legacy.Models.Repo`1 as well</remarks>\n"
                + "    void M() { }\n"
                + "}",
                true
                );

            Assert.Equal(2, matches.Count);

            var whole = matches.Single(m => m.MatchedText == "Legacy.Models.Customer");
            Assert.Equal(ConfidenceLevel.Possible, whole.Level);
            Assert.Equal(50, whole.Score.Value);

            Assert.DoesNotContain(matches, m => m.MatchedText == "Legacy.Models.Order");
        }

        // ---- the namespace of a Razor directive and the like ----

        [Fact]
        public void The_weights_are_data_the_levels_are_ranges()
        {
            Assert.Equal(ConfidenceLevel.Certain, Confidence.LevelOf(100));
            Assert.Equal(ConfidenceLevel.Certain, Confidence.LevelOf(90));
            Assert.Equal(ConfidenceLevel.Likely, Confidence.LevelOf(89));
            Assert.Equal(ConfidenceLevel.Likely, Confidence.LevelOf(70));
            Assert.Equal(ConfidenceLevel.Possible, Confidence.LevelOf(69));
            Assert.Equal(ConfidenceLevel.Possible, Confidence.LevelOf(45));
            Assert.Equal(ConfidenceLevel.Unlikely, Confidence.LevelOf(44));
            Assert.Equal(ConfidenceLevel.Unlikely, Confidence.LevelOf(0));
        }

        [Fact]
        public void The_score_is_put_into_the_range()
        {
            var high = new ConfidenceScore(new[] { new ConfidenceSignal("a", 95), new ConfidenceSignal("b", 40) });
            var low = new ConfidenceScore(new[] { new ConfidenceSignal("a", 30), new ConfidenceSignal("b", -90) });

            Assert.Equal(100, high.Value);
            Assert.Equal(0, low.Value);
        }
    }
}
