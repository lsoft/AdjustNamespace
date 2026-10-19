using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Adjusting.Report;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.TextReferences;
using AdjustNamespace.Tests.Infrastructure;
using System.Linq;
using System.Text;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// The text references in the files that are no C# documents (the project file, the Razor
    /// views, the settings files), through a whole session: the files are changed where the names
    /// are and nowhere else, with their encoding and line breaks, and the solution still compiles.
    /// </summary>
    public class TextFilesSessionTests
    {
        private const string ProgramSource =
@"namespace Legacy.Models
{
    public class Program
    {
        public static void Main()
        {
        }
    }
}
";

        private const string ProjectText =
            "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n"
            + "  <PropertyGroup>\r\n"
            + "    <StartupObject>Legacy.Models.Program</StartupObject>\r\n"
            + "    <RootNamespace>Legacy.Models</RootNamespace>\r\n"
            + "  </PropertyGroup>\r\n"
            + "</Project>\r\n";

        private const string ViewImportsText =
            "@using System\r\n"
            + "@using Legacy.Models\r\n"
            + "@model Legacy.Models.Program\r\n";

        private const string SettingsText =
            "<?xml version='1.0' encoding='utf-8'?>\n"
            + "<SettingsFile xmlns=\"http://schemas.microsoft.com/VisualStudio/2004/01/settings\" GeneratedClassNamespace=\"Legacy.Models\" GeneratedClassName=\"Settings\">\n"
            + "  <Profiles />\n"
            + "</SettingsFile>\n";

        private static TestSolution Solution()
        {
            var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Program.cs", ProgramSource)
                ;

            solution.AddTextFile("MyApp", "MyApp.csproj", ProjectText, new UTF8Encoding(true));
            solution.AddTextFile("MyApp", @"Views\_ViewImports.cshtml", ViewImportsText);
            solution.AddTextFile("MyApp", @"Properties\Settings.settings", SettingsText);

            return solution;
        }

        private static async System.Threading.Tasks.Task<AdjustReport> RunAsync(
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
                .RunAsync(new[] { solution.PathOf("MyApp", @"Models\Program.cs") });
        }

        [Fact]
        public async Task The_names_in_the_project_the_views_and_the_settings_follow_the_type()
        {
            using var solution = Solution();

            var report = await RunAsync(solution, TextReferenceOptions.Default);

            Assert.Equal(AdjustSessionOutcome.Completed, report.Outcome);

            Assert.Equal(
                ProjectText.Replace("<StartupObject>Legacy.Models.Program<", "<StartupObject>MyApp.Models.Program<"),
                solution.TextFileTextOf("MyApp", "MyApp.csproj")
                );

            Assert.Equal(
                "@using System\r\n@using MyApp.Models\r\n@model MyApp.Models.Program\r\n",
                solution.TextFileTextOf("MyApp", @"Views\_ViewImports.cshtml")
                );

            Assert.Equal(
                SettingsText.Replace("GeneratedClassNamespace=\"Legacy.Models\"", "GeneratedClassNamespace=\"MyApp.Models\""),
                solution.TextFileTextOf("MyApp", @"Properties\Settings.settings")
                );

            Assert.Empty(await solution.CompilationErrorsAsync());

            Assert.Equal(4, report.TextChangedCount);
            Assert.Equal(0, report.TextLeftCount);
        }

        [Fact]
        public async Task The_encoding_and_the_line_breaks_of_the_files_stay()
        {
            using var solution = Solution();

            var projectBefore = solution.XamlBytesOf("MyApp", "MyApp.csproj");
            var viewBefore = solution.XamlBytesOf("MyApp", @"Views\_ViewImports.cshtml");

            await RunAsync(solution, TextReferenceOptions.Default);

            var projectAfter = solution.XamlBytesOf("MyApp", "MyApp.csproj");
            var viewAfter = solution.XamlBytesOf("MyApp", @"Views\_ViewImports.cshtml");

            //the byte order mark of the project file
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, projectAfter.Take(3).ToArray());
            //`Legacy` -> `MyApp` is a byte shorter, and the only change
            Assert.Equal(projectBefore.Length - 1, projectAfter.Length);

            //`Legacy` -> `MyApp` is the only change: CRLF is intact
            Assert.Equal(Encoding.UTF8.GetString(viewBefore).Replace("Legacy", "MyApp"), Encoding.UTF8.GetString(viewAfter));
            Assert.DoesNotContain((byte)0xEF, viewAfter.Take(1));
        }

        [Fact]
        public async Task The_kinds_of_files_can_be_switched_off()
        {
            using var solution = Solution();

            var kinds = TextReferenceKinds.Default & ~TextReferenceKinds.Razor & ~TextReferenceKinds.ProjectFiles;

            var report = await RunAsync(solution, TextReferenceOptions.Default.WithKinds(kinds));

            Assert.Equal(ProjectText, solution.TextFileTextOf("MyApp", "MyApp.csproj"));
            Assert.Equal(ViewImportsText, solution.TextFileTextOf("MyApp", @"Views\_ViewImports.cshtml"));
            Assert.Equal(SettingsText, solution.TextFileTextOf("MyApp", @"Properties\Settings.settings"));
            Assert.Empty(report.TextReferences);
        }

        [Fact]
        public async Task The_conservative_mode_changes_the_certain_names_and_leaves_the_namespaces()
        {
            using var solution = Solution();

            var report = await RunAsync(solution, TextReferenceOptions.Default.WithMode(TextReferenceMode.Conservative));

            //Certain: the startup object and the model
            Assert.Contains("<StartupObject>MyApp.Models.Program<", solution.TextFileTextOf("MyApp", "MyApp.csproj"));
            Assert.Contains("@model MyApp.Models.Program", solution.TextFileTextOf("MyApp", @"Views\_ViewImports.cshtml"));

            //Likely (an emptied namespace is never certain): offered
            Assert.Contains("@using Legacy.Models", solution.TextFileTextOf("MyApp", @"Views\_ViewImports.cshtml"));
            Assert.Equal(SettingsText, solution.TextFileTextOf("MyApp", @"Properties\Settings.settings"));

            Assert.Equal(2, report.TextChangedCount);
            Assert.Equal(2, report.TextLeftCount);
        }

        [Fact]
        public async Task A_namespace_that_stays_alive_is_not_touched()
        {
            using var solution = Solution();

            solution.AddDocument("MyApp", @"Keeper\Keeper.cs", "namespace Legacy.Models { public class Keeper { } }");

            var report = await RunAsync(solution, TextReferenceOptions.Default);

            var views = solution.TextFileTextOf("MyApp", @"Views\_ViewImports.cshtml");

            //the type has moved, the namespace has not gone away
            Assert.Contains("@using Legacy.Models\r\n", views);
            Assert.Contains("@model MyApp.Models.Program", views);
            Assert.Equal(SettingsText, solution.TextFileTextOf("MyApp", @"Properties\Settings.settings"));

            Assert.Equal(2, report.TextChangedCount);
        }

        [Fact]
        public async Task A_text_file_that_has_changed_since_the_search_is_left_alone()
        {
            using var solution = Solution();

            var scan = await TextReferenceScanner.ScanAsync(
                solution.Context,
                new NamespaceReplaceRegex(string.Empty, string.Empty),
                new[] { solution.PathOf("MyApp", @"Models\Program.cs") },
                TextReferenceOptions.Default
                );

            //somebody edits the view between the search and the run
            var edited = "@* edited *@\r\n" + ViewImportsText;
            solution.AddTextFile("MyApp", @"Views\_ViewImports.cshtml", edited);

            var report = await new AdjustSession(
                    solution.Context,
                    new NamespaceReplaceRegex(string.Empty, string.Empty),
                    null,
                    null,
                    scan
                    )
                .RunAsync(new[] { solution.PathOf("MyApp", @"Models\Program.cs") });

            Assert.Equal(edited, solution.TextFileTextOf("MyApp", @"Views\_ViewImports.cshtml"));

            var stale = report.TextReferences.Where(t => t.Match.FileKind == TextReferenceFileKind.Razor).ToList();

            Assert.Equal(2, stale.Count);
            Assert.All(stale, t => Assert.Equal("the text has changed since the search", t.Reason));

            //the other files are changed as usual
            Assert.Contains("<StartupObject>MyApp.Models.Program<", solution.TextFileTextOf("MyApp", "MyApp.csproj"));
        }

        [Fact]
        public async Task The_changed_files_are_in_the_report_with_the_kind_of_the_text_reference()
        {
            using var solution = Solution();

            var report = await RunAsync(solution, TextReferenceOptions.Default);

            var project = report.TouchedFiles.Single(f => f.FilePath == solution.PathOf("MyApp", "MyApp.csproj"));

            Assert.Equal(EditKind.TextReference, Assert.Single(project.Edits).Key);

            var json = Newtonsoft.Json.Linq.JObject.Parse(report.ToJson());
            var items = (Newtonsoft.Json.Linq.JArray)json["textReferences"]!["items"]!;

            Assert.Contains(items, i => (string?)i["file"] == "Project" && (string?)i["place"] == "ProjectStartupObject");
            Assert.Contains(items, i => (string?)i["file"] == "Razor" && (string?)i["place"] == "RazorDirective");
            Assert.Contains(items, i => (string?)i["file"] == "Settings" && (string?)i["place"] == "SettingsNamespace");
        }

        [Fact]
        public async Task The_project_file_of_a_xaml_free_solution_is_found_through_the_solution_tree()
        {
            using var solution = Solution();

            var paths = await solution.Context.Solution.GetTextFilePathsAsync();

            Assert.Contains(solution.PathOf("MyApp", "MyApp.csproj"), paths);
            Assert.Contains(solution.PathOf("MyApp", @"Views\_ViewImports.cshtml"), paths);
        }
    }
}
