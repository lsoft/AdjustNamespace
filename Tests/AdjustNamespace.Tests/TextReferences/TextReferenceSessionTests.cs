using AdjustNamespace.Adjusting.Report;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.TextReferences;
using AdjustNamespace.Tests.Infrastructure;
using System.Linq;
using System.Threading;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// The text references through a whole session: the types are moved, the strings that name
    /// them follow, and the solution still compiles. The mode decides which levels follow.
    /// </summary>
    public class TextReferenceSessionTests
    {
        private const string CustomerSource =
@"namespace Legacy.Models
{
    public class Customer
    {
        public static string FullName => ""Legacy.Models.Customer"";
    }
}
";

        private const string FactorySource =
@"using System;

namespace Other
{
    public static class Factory
    {
        public const string Likely = ""Legacy.Models.Customer"";

        public const string Sentence = ""Could not find Legacy.Models.Customer anywhere"";

        /// <summary>Legacy.Models.Customer</summary>
        public static object Create() => Activator.CreateInstance(Type.GetType(""Legacy.Models.Customer, MyApp""));
    }
}
";

        private static TestSolution Solution()
        {
            return new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Customer.cs", CustomerSource)
                .AddDocument("MyApp", "Factory.cs", FactorySource)
                ;
        }

        private static async System.Threading.Tasks.Task<AdjustReport> RunAsync(
            TestSolution solution,
            TextReferenceOptions options,
            CancellationToken cancellationToken = default
            )
        {
            return await new AdjustSession(solution.Context, NoRegex(), null, options)
                .RunAsync(new[] { solution.PathOf("MyApp", @"Models\Customer.cs") }, null, cancellationToken);
        }

        private static AdjustNamespace.NamespaceReplaceRegex NoRegex()
        {
            return new AdjustNamespace.NamespaceReplaceRegex(string.Empty, string.Empty);
        }

        [Fact]
        public async Task The_default_mode_changes_the_certain_and_the_likely_names_and_the_solution_compiles()
        {
            using var solution = Solution();

            var report = await RunAsync(solution, TextReferenceOptions.Default);

            Assert.Equal(AdjustSessionOutcome.Completed, report.Outcome);

            var factory = solution.TextOf("MyApp", "Factory.cs");

            //a string passed as a type name, with the assembly
            Assert.Contains(@"Type.GetType(""MyApp.Models.Customer, MyApp"")", factory);
            //a string that is the name
            Assert.Contains(@"Likely = ""MyApp.Models.Customer"";", factory);
            //a sentence is unlikely, and a comment is not searched by default
            Assert.Contains(@"""Could not find Legacy.Models.Customer anywhere""", factory);
            Assert.Contains("<summary>Legacy.Models.Customer</summary>", factory);

            //the moved file names itself
            Assert.Contains(@"FullName => ""MyApp.Models.Customer"";", solution.TextOf("MyApp", @"Models\Customer.cs"));

            Assert.Empty(await solution.CompilationErrorsAsync());

            Assert.True(report.TextReferencesWereSearched);
            Assert.Equal(3, report.TextChangedCount);
            Assert.Equal(0, report.TextLeftCount);
            Assert.Equal(1, report.HiddenTextReferencesCount);
        }

        [Fact]
        public async Task Only_the_names_are_changed_and_nothing_around_them()
        {
            using var solution = Solution();

            var before = solution.TextOf("MyApp", "Factory.cs");

            await RunAsync(solution, TextReferenceOptions.Default);

            var expected = before
                .Replace(@"Likely = ""Legacy.Models.Customer"";", @"Likely = ""MyApp.Models.Customer"";")
                .Replace(@"Type.GetType(""Legacy.Models.Customer, MyApp"")", @"Type.GetType(""MyApp.Models.Customer, MyApp"")");

            Assert.Equal(expected, solution.TextOf("MyApp", "Factory.cs"));
        }

        [Fact]
        public async Task The_conservative_mode_changes_the_certain_names_only_and_offers_the_rest()
        {
            using var solution = Solution();

            var report = await RunAsync(
                solution,
                TextReferenceOptions.Default.WithMode(TextReferenceMode.Conservative)
                );

            var factory = solution.TextOf("MyApp", "Factory.cs");

            Assert.Contains(@"Type.GetType(""MyApp.Models.Customer, MyApp"")", factory);
            Assert.Contains(@"Likely = ""Legacy.Models.Customer"";", factory);

            Assert.Equal(1, report.TextChangedCount);
            Assert.Equal(2, report.TextLeftCount);

            var left = report.TextReferences.Where(t => !t.IsChanged).ToList();
            Assert.All(left, t => Assert.Equal(ConfidenceLevel.Likely, t.Match.Level));
            Assert.All(left, t => Assert.Contains("only offered in the Conservative mode", t.Reason));

            Assert.True(report.HasLeftovers);
        }

        [Fact]
        public async Task The_report_only_mode_changes_nothing()
        {
            using var solution = Solution();

            var before = solution.TextOf("MyApp", "Factory.cs");

            var report = await RunAsync(
                solution,
                TextReferenceOptions.Default.WithMode(TextReferenceMode.ReportOnly)
                );

            Assert.Equal(before, solution.TextOf("MyApp", "Factory.cs"));
            Assert.Equal(0, report.TextChangedCount);
            Assert.Equal(3, report.TextLeftCount);
            Assert.All(report.TextReferences, t => Assert.Equal("the mode is report only", t.Reason));

            //the types themselves are moved as usual
            Assert.Contains("namespace MyApp.Models", solution.TextOf("MyApp", @"Models\Customer.cs"));
        }

        [Fact]
        public async Task The_off_mode_does_not_search_at_all()
        {
            using var solution = Solution();

            var before = solution.TextOf("MyApp", "Factory.cs");

            var report = await RunAsync(solution, TextReferenceOptions.Off);

            Assert.Equal(before, solution.TextOf("MyApp", "Factory.cs"));
            Assert.False(report.TextReferencesWereSearched);
            Assert.Empty(report.TextReferences);
            Assert.DoesNotContain("Text references", report.ToMarkdown());
        }

        [Fact]
        public async Task A_session_without_the_options_does_not_search()
        {
            using var solution = Solution();

            var before = solution.TextOf("MyApp", "Factory.cs");

            var report = await new AdjustSession(solution.Context, NoRegex())
                .RunAsync(new[] { solution.PathOf("MyApp", @"Models\Customer.cs") });

            Assert.Equal(before, solution.TextOf("MyApp", "Factory.cs"));
            Assert.False(report.TextReferencesWereSearched);
        }

        [Fact]
        public async Task The_comments_are_changed_when_they_are_asked_for_and_the_mode_is_aggressive()
        {
            using var solution = Solution();

            var options = new TextReferenceOptions(TextReferenceMode.Aggressive, TextReferenceKinds.All);

            var report = await RunAsync(solution, options);

            var factory = solution.TextOf("MyApp", "Factory.cs");

            Assert.Contains("<summary>MyApp.Models.Customer</summary>", factory);
            //the sentence is still unlikely, whatever the mode is
            Assert.Contains(@"""Could not find Legacy.Models.Customer anywhere""", factory);

            Assert.Equal(4, report.TextChangedCount);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        [Fact]
        public async Task A_comment_is_offered_and_left_in_the_balanced_mode()
        {
            using var solution = Solution();

            var options = new TextReferenceOptions(TextReferenceMode.Balanced, TextReferenceKinds.All);

            var report = await RunAsync(solution, options);

            Assert.Contains("<summary>Legacy.Models.Customer</summary>", solution.TextOf("MyApp", "Factory.cs"));

            var left = Assert.Single(report.TextReferences, t => !t.IsChanged);

            Assert.Equal(ConfidenceLevel.Possible, left.Match.Level);
            Assert.Equal(TextReferencePlace.Comment, left.Match.Place);
        }

        [Fact]
        public async Task The_line_breaks_and_the_rest_of_the_file_stay_as_they_are()
        {
            var source = "namespace Other\r\n{\r\n\tpublic class Names\r\n\t{\r\n\t\tpublic const string A = \"Legacy.Models.Customer\";\r\n\t}\r\n}\r\n";

            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Customer.cs", CustomerSource)
                .AddDocument("MyApp", "Names.cs", source)
                ;

            await RunAsync(solution, TextReferenceOptions.Default);

            Assert.Equal(
                source.Replace("Legacy.Models.Customer", "MyApp.Models.Customer"),
                solution.TextOf("MyApp", "Names.cs")
                );
        }

        [Fact]
        public async Task A_name_the_scan_has_found_is_left_when_the_text_has_changed_since()
        {
            using var solution = Solution();

            var scan = await TextReferenceScanner.ScanAsync(
                solution.Context,
                NoRegex(),
                new[] { solution.PathOf("MyApp", @"Models\Customer.cs") },
                TextReferenceOptions.Default
                );

            //the user types between the scan and the run, the spans of the scan are stale
            solution.ReplaceDocument("MyApp", "Factory.cs", "// a new first line\n" + FactorySource);

            var report = await new AdjustSession(solution.Context, NoRegex(), null, null, scan)
                .RunAsync(new[] { solution.PathOf("MyApp", @"Models\Customer.cs") });

            Assert.Equal(AdjustSessionOutcome.Completed, report.Outcome);

            var stale = report.TextReferences
                .Where(t => t.Match.FilePath == solution.PathOf("MyApp", "Factory.cs"))
                .ToList();

            Assert.NotEmpty(stale);
            Assert.All(stale, t => Assert.False(t.IsChanged));
            Assert.All(stale, t => Assert.Equal("the text has changed since the search", t.Reason));

            //the file is as the user left it
            Assert.Equal("// a new first line\n" + FactorySource, solution.TextOf("MyApp", "Factory.cs"));

            //what has not changed is still changed
            Assert.Contains(@"FullName => ""MyApp.Models.Customer"";", solution.TextOf("MyApp", @"Models\Customer.cs"));
        }

        [Fact]
        public async Task The_choice_of_the_user_in_the_review_wins()
        {
            using var solution = Solution();

            var scan = await TextReferenceScanner.ScanAsync(
                solution.Context,
                NoRegex(),
                new[] { solution.PathOf("MyApp", @"Models\Customer.cs") },
                TextReferenceOptions.Default
                );

            //the user unchecks the one the mode would change, and checks the one it hides
            var uncheck = scan.Matches.Single(m => m.MatchedText == "Legacy.Models.Customer" && m.LineText.Contains("Likely ="));
            uncheck.IsApproved = false;

            var hidden = scan.Matches.Single(m => m.Level == ConfidenceLevel.Unlikely);
            hidden.IsApproved = true;

            var report = await new AdjustSession(solution.Context, NoRegex(), null, null, scan)
                .RunAsync(new[] { solution.PathOf("MyApp", @"Models\Customer.cs") });

            var factory = solution.TextOf("MyApp", "Factory.cs");

            Assert.Contains(@"Likely = ""Legacy.Models.Customer"";", factory);
            Assert.Contains(@"""Could not find MyApp.Models.Customer anywhere""", factory);

            var left = Assert.Single(report.TextReferences, t => !t.IsChanged);
            Assert.Equal("unchecked", left.Reason);
        }

        [Fact]
        public async Task A_cancelled_run_changes_no_text()
        {
            using var solution = Solution();

            var before = solution.TextOf("MyApp", "Factory.cs");

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var report = await RunAsync(solution, TextReferenceOptions.Default, cts.Token);

            Assert.Equal(AdjustSessionOutcome.Cancelled, report.Outcome);
            Assert.Equal(before, solution.TextOf("MyApp", "Factory.cs"));
        }

        [Fact]
        public async Task The_markdown_and_the_json_of_the_report_list_the_text_references()
        {
            using var solution = Solution();

            var report = await RunAsync(solution, new TextReferenceOptions(TextReferenceMode.Balanced, TextReferenceKinds.All));

            var markdown = report.ToMarkdown();

            Assert.Contains("Text references: changed 3, left 1.", markdown);
            Assert.Contains("## Text references", markdown);
            Assert.Contains("### Changed (3)", markdown);
            Assert.Contains("### Left (1)", markdown);
            Assert.Contains(@"`MyApp\Factory.cs:", markdown);
            Assert.Contains("`Legacy.Models.Customer` -> `MyApp.Models.Customer` [Certain 100]", markdown);
            Assert.Contains("string passed as a type name +95", markdown);
            Assert.Contains("1 unlikely not shown", markdown);

            var json = Newtonsoft.Json.Linq.JObject.Parse(report.ToJson());

            Assert.Equal(3, (int)json["textReferences"]!["changed"]!);
            Assert.Equal(1, (int)json["textReferences"]!["left"]!);
            Assert.Equal(1, (int)json["textReferences"]!["hiddenUnlikely"]!);

            var items = (Newtonsoft.Json.Linq.JArray)json["textReferences"]!["items"]!;
            var certain = items.First(i => (string?)i["level"] == "Certain");

            Assert.Equal(100, (int)certain["score"]!);
            Assert.Equal("string passed as a type name", (string?)certain["signals"]![0]!["name"]);
            Assert.Equal(95, (int)certain["signals"]![0]!["weight"]!);
        }

        [Fact]
        public async Task The_text_references_are_in_the_sections_of_the_wizard()
        {
            using var solution = Solution();

            var report = await RunAsync(solution, new TextReferenceOptions(TextReferenceMode.Balanced, TextReferenceKinds.All));

            var sections = AdjustReportSections.Build(report);

            var changed = sections.Single(s => s.Title == "Text references changed (3)");
            var left = sections.Single(s => s.Title == "Text references left (1)");

            Assert.All(changed.Children, c => Assert.NotNull(c.FilePath));
            Assert.All(changed.Children, c => Assert.True(c.Line > 0));
            Assert.Contains(changed.Children, c => c.Detail!.StartsWith("Legacy.Models.Customer -> MyApp.Models.Customer [Certain 100]"));
            Assert.Contains("comment", left.Children.Single().ToolTip);
        }
    }
}
