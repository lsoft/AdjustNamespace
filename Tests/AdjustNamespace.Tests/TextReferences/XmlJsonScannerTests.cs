using AdjustNamespace.TextReferences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// Tests of the scanners of the xml configs and of the json files, with the score they give:
    /// the part of the corpus that is neither C# nor Razor.
    /// </summary>
    public class XmlJsonScannerTests
    {
        private const string Config = @"C:\sln\App\app.config";
        private const string Settings = @"C:\sln\App\appsettings.json";

        private static List<TextReferenceMatch> Xml(string text, bool comments = false)
        {
            var kinds = comments ? TextReferenceKinds.All : TextReferenceKinds.Default;

            return TextReferenceScanner.ScanText(
                TextReferenceCorpus.Set(),
                TextReferenceFileKind.XmlConfig,
                Config,
                text,
                new TextReferenceOptions(TextReferenceMode.Balanced, kinds)
                );
        }

        private static List<TextReferenceMatch> Json(string text, string path = Settings)
        {
            return TextReferenceScanner.ScanText(
                TextReferenceCorpus.Set(),
                TextReferenceFileKind.Json,
                path,
                text,
                TextReferenceOptions.Default
                );
        }

        // ---- xml ----

        [Theory]
        [InlineData("<section name=\"x\" type=\"Legacy.Models.Customer, Lib\" />", 100)]
        [InlineData("<section name=\"x\" type=\"Legacy.Models.Customer\" />", 95)]
        [InlineData("<target name=\"t\" xsi:type=\"Legacy.Models.Customer\" />", 95)]
        [InlineData("<sink typeName=\"Legacy.Models.Order, Lib, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" />", 100)]
        [InlineData("<register type=\"Legacy.Models.Customer\" mapTo=\"Legacy.Models.Order\" />", 95)]
        [InlineData("<endpoint contract=\"Legacy.Models.Customer\" binding=\"basicHttpBinding\" />", 95)]
        [InlineData("<objectFactory factory=\"Legacy.Models.Customer\" />", 95)]
        [InlineData("<service name=\"Legacy.Models.Order\"><host /></service>", 95)]
        [InlineData("<type>Legacy.Models.Customer, Lib</type>", 100)]
        [InlineData("<typeName>Legacy.Models.Customer</typeName>", 95)]
        public void The_attributes_that_hold_a_type_name_are_certain(string xml, int score)
        {
            var matches = Xml(xml);

            Assert.NotEmpty(matches);
            Assert.All(matches, m => Assert.Equal(TextReferencePlace.XmlTypeAttribute, m.Place));
            Assert.Equal(score, matches[0].Score.Value);
            Assert.Equal(ConfidenceLevel.Certain, matches[0].Level);
        }

        [Fact]
        public void The_name_of_an_element_other_than_a_service_is_just_a_value()
        {
            var match = Assert.Single(Xml("<logger name=\"Legacy.Models.Customer\" />"));

            Assert.Equal(TextReferencePlace.OtherValue, match.Place);
            Assert.Equal(60, match.Score.Value);
            Assert.Equal(ConfidenceLevel.Possible, match.Level);
        }

        [Theory]
        [InlineData("<add key=\"Handler\" value=\"Legacy.Models.Customer, Lib\" />", ConfidenceLevel.Likely, 80)]
        [InlineData("<add key=\"Handler\" value=\"Legacy.Models.Customer\" />", ConfidenceLevel.Possible, 60)]
        [InlineData("<Handler>Legacy.Models.Customer</Handler>", ConfidenceLevel.Possible, 60)]
        [InlineData("<add key=\"Handler\" value=\"Legacy.Models.Customer, OtherLib\" />", ConfidenceLevel.Unlikely, 20)]
        [InlineData("<add key=\"Message\" value=\"Legacy.Models.Customer was not found\" />", ConfidenceLevel.Unlikely, 20)]
        [InlineData("<add key=\"Path\" value=\"C:\\data\\Legacy.Models.Customer.xml\" />", ConfidenceLevel.Unlikely, -10)]
        public void Any_other_value_is_worth_less(string xml, ConfidenceLevel level, int unclampedScore)
        {
            var match = Assert.Single(Xml(xml));

            Assert.Equal(TextReferencePlace.OtherValue, match.Place);
            Assert.Equal(level, match.Level);
            Assert.Equal(System.Math.Max(0, unclampedScore), match.Score.Value);
        }

        [Fact]
        public void An_assembly_of_another_type_makes_a_type_attribute_possible_only()
        {
            var match = Assert.Single(Xml("<section type=\"Legacy.Models.Customer, OtherLib\" />"));

            Assert.Equal(55, match.Score.Value);
            Assert.Equal(ConfidenceLevel.Possible, match.Level);
        }

        [Fact]
        public void A_generic_type_with_a_type_argument_names_both_types()
        {
            var matches = Xml("<section type=\"Legacy.Models.Repo`1[[Legacy.Models.Customer, Lib]], Lib\" />");

            Assert.Equal(new[] { "Legacy.Models.Repo", "Legacy.Models.Customer" }, matches.Select(m => m.MatchedText).ToArray());
            Assert.All(matches, m => Assert.Equal(ConfidenceLevel.Certain, m.Level));
        }

        [Fact]
        public void The_positions_of_a_name_in_a_config_are_exact()
        {
            var xml =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n"
                + "<configuration>\r\n"
                + "  <!-- the handler -->\r\n"
                + "  <handlers>\r\n"
                + "    <add name='h' type='Legacy.Models.Customer, Lib' />\r\n"
                + "  </handlers>\r\n"
                + "</configuration>\r\n";

            var match = Assert.Single(Xml(xml));

            Assert.Equal(5, match.Line);
            Assert.Equal("    <add name='h' type='".Length, match.Column);
            Assert.Equal("Legacy.Models.Customer", xml.Substring(match.Span.Start, match.Span.Length));
            Assert.Equal("    <add name='h' type='Legacy.Models.Customer, Lib' />", match.LineText);
        }

        [Fact]
        public void A_comment_of_an_xml_config_is_searched_only_when_asked_for()
        {
            var xml = "<a><!-- Legacy.Models.Customer --></a>";

            Assert.Empty(Xml(xml));

            var match = Assert.Single(Xml(xml, comments: true));

            Assert.Equal(TextReferencePlace.Comment, match.Place);
            Assert.Equal(50, match.Score.Value);
        }

        [Fact]
        public void What_is_not_a_value_is_no_match()
        {
            var matches = Xml("<Legacy.Models.Customer attr=\"x\"><CustomerExtra a=\"Legacy.Models.CustomerExtra\" /></Legacy.Models.Customer>");

            Assert.Empty(matches);
        }

        // ---- json ----

        [Theory]
        [InlineData("{ \"$type\": \"Legacy.Models.Customer, Lib\" }", 100)]
        [InlineData("{ \"$type\": \"Legacy.Models.Customer\" }", 90)]
        [InlineData("{ \"Type\": \"Legacy.Models.Customer\" }", 90)]
        [InlineData("{ \"class\": \"Legacy.Models.Customer\" }", 90)]
        [InlineData("{ \"Serilog\": { \"Using\": [ \"Legacy.Models.Customer\" ] } }", 90)]
        [InlineData("{ \"a\": { \"b\": { \"type\": \"Legacy.Models.Customer, Lib\" } } }", 100)]
        public void A_value_of_a_key_that_holds_a_type_name_is_certain(string json, int score)
        {
            var match = Assert.Single(Json(json));

            Assert.Equal(TextReferencePlace.JsonTypeValue, match.Place);
            Assert.Equal(score, match.Score.Value);
            Assert.Equal(ConfidenceLevel.Certain, match.Level);
        }

        [Theory]
        [InlineData("{ \"Handler\": \"Legacy.Models.Customer\" }", ConfidenceLevel.Possible, 60)]
        [InlineData("{ \"Handler\": \"Legacy.Models.Customer, Lib\" }", ConfidenceLevel.Likely, 80)]
        [InlineData("{ \"Handlers\": [ \"Legacy.Models.Customer\" ] }", ConfidenceLevel.Possible, 60)]
        [InlineData("{ \"Message\": \"see Legacy.Models.Customer for details\" }", ConfidenceLevel.Unlikely, 20)]
        [InlineData("{ \"Logo\": \"images/Legacy.Models.Customer.png\" }", ConfidenceLevel.Unlikely, 0)]
        public void Any_other_value_is_worth_less_in_json_too(string json, ConfidenceLevel level, int score)
        {
            var match = Assert.Single(Json(json));

            Assert.Equal(TextReferencePlace.OtherValue, match.Place);
            Assert.Equal(level, match.Level);
            Assert.Equal(score, match.Score.Value);
        }

        [Fact]
        public void A_key_is_not_searched_and_a_value_after_it_is()
        {
            var json = "{ \"Legacy.Models.Customer\": \"Legacy.Models.Customer\" }";

            var match = Assert.Single(Json(json));

            Assert.Equal(json.LastIndexOf("Legacy.Models.Customer"), match.Span.Start);
        }

        [Fact]
        public void The_comments_of_a_json_are_skipped()
        {
            var json =
                "{\n"
                + "  // \"type\": \"Legacy.Models.Customer\"\n"
                + "  /* \"type\": \"Legacy.Models.Order\" */\n"
                + "  \"type\": \"Legacy.Models.Repo`1\"\n"
                + "}";

            var match = Assert.Single(Json(json));

            Assert.Equal("Legacy.Models.Repo", match.MatchedText);
            Assert.Equal(4, match.Line);
        }

        [Fact]
        public void An_escaped_quote_does_not_end_the_string_and_the_span_stays_exact()
        {
            var json = "{ \"type\": \"say \\\"hi\\\" to Legacy.Models.Customer\", \"other\": \"Legacy.Models.Order\" }";

            var matches = Json(json);

            Assert.Equal(2, matches.Count);
            Assert.All(matches, m => Assert.Equal(m.MatchedText, json.Substring(m.Span.Start, m.Span.Length)));
        }

        [Fact]
        public void A_type_in_a_dollar_type_comes_with_a_note_about_the_serialized_data()
        {
            var match = Assert.Single(Json("{ \"$type\": \"Legacy.Models.Customer, Lib\" }"));

            Assert.Equal(JsonTextScanner.SerializedTypeNote, match.Note);

            var other = Assert.Single(Json("{ \"type\": \"Legacy.Models.Customer, Lib\" }"));

            Assert.Null(other.Note);
        }

        [Fact]
        public void A_broken_json_is_read_as_far_as_it_goes()
        {
            foreach (var json in new[] { "{ \"type\": \"Legacy.Models.Customer", "{ \"a\": [ \"Legacy.Models.Order\"", "}}]]\"", "\"", "{ \"a\"", "/*" })
            {
                var matches = Json(json);

                Assert.All(matches, m => Assert.True(m.Span.End <= json.Length));
            }
        }

        // ---- the files the settings name ----

        [Theory]
        [InlineData(@"C:\sln\App\app.config", true)]
        [InlineData(@"C:\sln\App\Web.Release.config", true)]
        [InlineData(@"C:\sln\App\NLog.config", true)]
        [InlineData(@"C:\sln\App\appsettings.json", true)]
        [InlineData(@"C:\sln\App\appsettings.Development.json", true)]
        [InlineData(@"C:\sln\App\package.json", false)]
        [InlineData(@"C:\sln\App\launchSettings.json", false)]
        [InlineData(@"C:\sln\App\Services.xml", false)]
        public void The_usual_configs_are_searched_and_the_other_files_are_not(string path, bool searched)
        {
            Assert.Equal(searched, TextReferenceFiles.Classify(path, TextReferenceOptions.Default, @"C:\sln") != null);
        }

        [Fact]
        public void The_files_named_by_the_settings_are_searched_too()
        {
            var options = new TextReferenceOptions(
                TextReferenceMode.Balanced,
                TextReferenceKinds.Default,
                new[] { "Services.xml", @"App\Config\*.json" }
                );

            Assert.Equal(TextReferenceFileKind.XmlConfig, TextReferenceFiles.Classify(@"C:\sln\App\Services.xml", options, @"C:\sln"));
            Assert.Equal(TextReferenceFileKind.Json, TextReferenceFiles.Classify(@"C:\sln\App\Config\handlers.json", options, @"C:\sln"));
            Assert.Null(TextReferenceFiles.Classify(@"C:\sln\App\Other.xml", options, @"C:\sln"));
            Assert.Null(TextReferenceFiles.Classify(@"C:\sln\App\package.json", options, @"C:\sln"));
        }

        [Fact]
        public void The_switches_of_xml_and_json_work()
        {
            var noXml = TextReferenceOptions.Default.WithKinds(TextReferenceKinds.Default & ~TextReferenceKinds.XmlConfigs);
            var noJson = TextReferenceOptions.Default.WithKinds(TextReferenceKinds.Default & ~TextReferenceKinds.Json);

            Assert.Null(TextReferenceFiles.Classify(Config, noXml, @"C:\sln"));
            Assert.NotNull(TextReferenceFiles.Classify(Settings, noXml, @"C:\sln"));

            Assert.Null(TextReferenceFiles.Classify(Settings, noJson, @"C:\sln"));
            Assert.NotNull(TextReferenceFiles.Classify(Config, noJson, @"C:\sln"));
        }

        [Fact]
        public void Test_data_is_worth_less_in_a_config_too()
        {
            var match = Assert.Single(
                TextReferenceScanner.ScanText(
                    TextReferenceCorpus.Set(),
                    TextReferenceFileKind.XmlConfig,
                    @"C:\sln\App.Tests\Fixtures\app.config",
                    "<section type=\"Legacy.Models.Customer\" />",
                    TextReferenceOptions.Default
                    )
                );

            Assert.Equal(75, match.Score.Value);
        }
    }
}
