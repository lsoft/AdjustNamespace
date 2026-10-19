using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.TextReferences;
using AdjustNamespace.Tests.Infrastructure;
using System.Linq;
using System.Text;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// The text references in the xml configs and in the json files, through a whole session.
    /// </summary>
    public class ConfigFilesSessionTests
    {
        private const string CustomerSource =
@"namespace Legacy.Models
{
    public class Customer { }
}
";

        private const string ConfigText =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n"
            + "<configuration>\r\n"
            + "  <appSettings>\r\n"
            + "    <add key=\"Handler\" value=\"Legacy.Models.Customer, MyApp\" />\r\n"
            + "    <add key=\"Message\" value=\"Legacy.Models.Customer was not found\" />\r\n"
            + "  </appSettings>\r\n"
            + "  <section name=\"s\" type=\"Legacy.Models.Customer, MyApp\" />\r\n"
            + "</configuration>\r\n";

        private const string JsonText =
            "{\n"
            + "  \"Handler\": { \"$type\": \"Legacy.Models.Customer, MyApp\" },\n"
            + "  \"Serilog\": { \"Using\": [ \"Legacy.Models.Customer\" ] },\n"
            + "  \"Comment\": \"see Legacy.Models.Customer\"\n"
            + "}\n";

        private static TestSolution Solution()
        {
            var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Customer.cs", CustomerSource);

            solution.AddTextFile("MyApp", "app.config", ConfigText, new UTF8Encoding(true));
            solution.AddTextFile("MyApp", "appsettings.json", JsonText);

            return solution;
        }

        private static async System.Threading.Tasks.Task<AdjustNamespace.Adjusting.Report.AdjustReport> RunAsync(
            TestSolution solution,
            TextReferenceOptions options
            )
        {
            return await new AdjustSession(
                    solution.Context,
                    new NamespaceReplaceRegex(string.Empty, string.Empty),
                    null,
                    options
                    )
                .RunAsync(new[] { solution.PathOf("MyApp", @"Models\Customer.cs") });
        }

        [Fact]
        public async Task The_names_in_a_config_and_in_a_json_file_follow_the_type()
        {
            using var solution = Solution();

            var report = await RunAsync(solution, TextReferenceOptions.Default);

            Assert.Equal(
                ConfigText.Replace("Legacy.Models.Customer, MyApp", "MyApp.Models.Customer, MyApp"),
                solution.TextFileTextOf("MyApp", "app.config")
                );

            Assert.Equal(
                "{\n"
                + "  \"Handler\": { \"$type\": \"MyApp.Models.Customer, MyApp\" },\n"
                + "  \"Serilog\": { \"Using\": [ \"MyApp.Models.Customer\" ] },\n"
                + "  \"Comment\": \"see Legacy.Models.Customer\"\n"
                + "}\n",
                solution.TextFileTextOf("MyApp", "appsettings.json")
                );

            //the byte order mark of the config stays
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, solution.XamlBytesOf("MyApp", "app.config").Take(3).ToArray());

            Assert.Empty(await solution.CompilationErrorsAsync());
            Assert.Equal(4, report.TextChangedCount);
        }

        [Fact]
        public async Task A_dollar_type_adds_a_note_about_the_serialized_data_to_the_report()
        {
            using var solution = Solution();

            var report = await RunAsync(solution, TextReferenceOptions.Default);

            Assert.Contains(report.Notes, n => n.Contains("$type"));
            Assert.Contains("## Notes", report.ToMarkdown());
        }

        [Fact]
        public async Task The_kinds_can_be_switched_off_one_by_one()
        {
            using var solution = Solution();

            await RunAsync(
                solution,
                TextReferenceOptions.Default.WithKinds(TextReferenceKinds.Default & ~TextReferenceKinds.XmlConfigs)
                );

            Assert.Equal(ConfigText, solution.TextFileTextOf("MyApp", "app.config"));
            Assert.Contains("MyApp.Models.Customer", solution.TextFileTextOf("MyApp", "appsettings.json"));

            using var second = Solution();

            await RunAsync(
                second,
                TextReferenceOptions.Default.WithKinds(TextReferenceKinds.Default & ~TextReferenceKinds.Json)
                );

            Assert.Equal(JsonText, second.TextFileTextOf("MyApp", "appsettings.json"));
            Assert.Contains("MyApp.Models.Customer", second.TextFileTextOf("MyApp", "app.config"));
        }

        [Fact]
        public async Task A_file_named_by_the_settings_is_searched()
        {
            using var solution = Solution();
            solution.AddTextFile("MyApp", "Services.xml", "<services><service type=\"Legacy.Models.Customer\" /></services>");

            await RunAsync(
                solution,
                new TextReferenceOptions(TextReferenceMode.Balanced, TextReferenceKinds.Default, new[] { "Services.xml" })
                );

            Assert.Equal(
                "<services><service type=\"MyApp.Models.Customer\" /></services>",
                solution.TextFileTextOf("MyApp", "Services.xml")
                );
        }

        [Fact]
        public async Task An_unnamed_xml_file_is_left_alone()
        {
            using var solution = Solution();
            const string Text = "<services><service type=\"Legacy.Models.Customer\" /></services>";
            solution.AddTextFile("MyApp", "Services.xml", Text);

            await RunAsync(solution, TextReferenceOptions.Default);

            Assert.Equal(Text, solution.TextFileTextOf("MyApp", "Services.xml"));
        }
    }
}
