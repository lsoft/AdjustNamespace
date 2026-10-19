using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Adjusting.Report;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.VisualStudio;
using AdjustNamespace.Xaml.BodyProvider;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of <see cref="AdjustReport"/>: what a session tells about the run. The report is
    /// the only thing the user sees after the run, so it has to be right about the files that
    /// were adjusted, about those that were not and about the files changed on the way.
    /// </summary>
    public class AdjustReportTests
    {
        [Fact]
        public async Task The_report_names_the_adjusted_file_and_its_namespaces()
        {
            using var solution = OneClassWithConsumer();

            var report = await RunAsync(solution, solution.PathOf("MyApp", @"First\Class1.cs"));

            Assert.Equal(AdjustSessionOutcome.Completed, report.Outcome);
            Assert.Equal(1, report.AdjustedCount);

            var file = report.Files.Single(f => f.Status == AdjustFileStatus.Adjusted);

            Assert.Equal(solution.PathOf("MyApp", @"First\Class1.cs"), file.FilePath);
            Assert.Equal("A.B", file.Changes.Single().Old);
            Assert.Equal("MyApp.First", file.Changes.Single().New);
        }

        [Fact]
        public async Task The_edits_in_the_other_files_are_counted_by_kind()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"First\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "WithUsing.cs",
@"using A.B;

namespace Other
{
    public class WithUsing
    {
        public Class1 Create() => new Class1();
    }
}
")
                .AddDocument("MyApp", "Qualified.cs",
@"namespace Other
{
    public class Qualified
    {
        public A.B.Class1 Create() => new A.B.Class1();
    }
}
")
                ;

            var report = await RunAsync(solution, solution.PathOf("MyApp", @"First\Class1.cs"));

            Assert.Empty(await solution.CompilationErrorsAsync());

            var file = report.Files.Single();

            var withUsing = file.EditedFiles.Single(f => f.FilePath == solution.PathOf("MyApp", "WithUsing.cs"));
            Assert.Contains(withUsing.Edits, e => e.Key == EditKind.UsingAdded && e.Value == 1);

            var qualified = file.EditedFiles.Single(f => f.FilePath == solution.PathOf("MyApp", "Qualified.cs"));
            Assert.Contains(qualified.Edits, e => e.Key == EditKind.NameQualified && e.Value == 2);

            //the subject file itself is edited too, but it is not "another file"
            var subject = file.EditedFiles.Single(f => f.FilePath == solution.PathOf("MyApp", @"First\Class1.cs"));
            Assert.Contains(subject.Edits, e => e.Key == EditKind.NamespaceMoved);

            Assert.Equal(1 + 2, file.EditsInOtherFiles);
        }

        /// <summary>
        /// The using clause of the emptied namespace is removed by the cleanup, which belongs to
        /// no particular file: the report keeps it apart.
        /// </summary>
        [Fact]
        public async Task The_cleanup_is_reported_apart()
        {
            using var solution = OneClassWithConsumer();

            var report = await RunAsync(solution, solution.PathOf("MyApp", @"First\Class1.cs"));

            var consumer = report.CleanupFiles.Single();

            Assert.Equal(solution.PathOf("MyApp", "Consumer.cs"), consumer.FilePath);
            Assert.Contains(consumer.Edits, e => e.Key == EditKind.UsingRemoved && e.Value == 1);

            //every touched file is there once, with the edits of every stage
            var touched = report.TouchedFiles.Single(f => f.FilePath == consumer.FilePath);
            Assert.Contains(touched.Edits, e => e.Key == EditKind.UsingAdded);
            Assert.Contains(touched.Edits, e => e.Key == EditKind.UsingRemoved);
        }

        [Fact]
        public async Task A_nameof_of_an_emptied_namespace_is_reported()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"First\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Names.cs",
@"namespace Other
{
    public class Names
    {
        public string Value => nameof(A.B);
    }
}
")
                ;

            var report = await RunAsync(solution, solution.PathOf("MyApp", @"First\Class1.cs"));

            var names = report.CleanupFiles.Single(f => f.FilePath == solution.PathOf("MyApp", "Names.cs"));

            Assert.Contains(names.Edits, e => e.Key == EditKind.NameofReplaced);
        }

        [Fact]
        public async Task A_xaml_file_is_reported_with_the_kind_of_its_change()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"First\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                ;

            var viewPath = solution.AddXamlFile("MyApp", @"Views\Lonely.xaml",
@"<UserControl x:Class=""Old.Lonely""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>");

            var usingPath = solution.AddXamlFile("MyApp", @"Resources.xaml",
@"<ResourceDictionary
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:ab=""clr-namespace:A.B"">
    <Style x:Key=""Key"" TargetType=""{x:Type ab:Class1}"" />
</ResourceDictionary>");

            var report = await RunAsync(
                solution,
                solution.PathOf("MyApp", @"First\Class1.cs"),
                viewPath
                );

            var view = report.Files.Single(f => f.FilePath == viewPath);
            Assert.Equal(AdjustFileStatus.Adjusted, view.Status);
            Assert.Equal("Old", view.Changes.Single().Old);
            Assert.Equal("MyApp.Views", view.Changes.Single().New);
            Assert.Contains(
                view.EditedFiles.Single().Edits,
                e => e.Key == EditKind.XamlClassMoved
                );

            var cs = report.Files.Single(f => f.FilePath == solution.PathOf("MyApp", @"First\Class1.cs"));
            Assert.Contains(
                cs.EditedFiles,
                f => f.FilePath == usingPath && f.Edits.Any(e => e.Key == EditKind.XamlReferencesUpdated)
                );
        }

        [Fact]
        public async Task A_file_with_nothing_to_move_is_reported_as_skipped()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Settled.cs",
@"namespace MyApp
{
    public class Settled { }
}
")
                ;

            var report = await RunAsync(solution, solution.PathOf("MyApp", "Settled.cs"));

            Assert.Equal(1, report.SkippedCount);
            Assert.Equal(0, report.AdjustedCount);
            Assert.Empty(report.TouchedFiles);
            Assert.False(report.HasLeftovers);
        }

        [Fact]
        public async Task The_files_blocked_before_the_run_are_in_the_report()
        {
            using var solution = OneClassWithConsumer();

            var blocked = AdjustBlock.Create(solution.PathOf("MyApp", "Consumer.cs"), AdjustBlockKind.CompiledBySeveralProjects);

            var report = await new AdjustSession(solution.Context, NoRegex(), new[] { blocked })
                .RunAsync(new[] { solution.PathOf("MyApp", @"First\Class1.cs") });

            Assert.Equal(1, report.BlockedCount);
            Assert.Equal(1, report.AdjustedCount);
            Assert.True(report.HasLeftovers);

            var file = report.Files.Single(f => f.Status == AdjustFileStatus.Blocked);

            Assert.Equal(blocked.Message, file.Message);
            Assert.Equal(AdjustBlockKind.CompiledBySeveralProjects, file.Block!.Value.Kind);
        }

        /// <summary>
        /// A file the planner blocks at the moment of the run (it has changed since the scan)
        /// is a blocked file, not a skipped one.
        /// </summary>
        [Fact]
        public async Task A_file_which_the_planner_blocks_during_the_run_is_reported_as_blocked()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddSharedProject("Common")
                .AddProject("Other")
                .AddSharedDocument("Common", "Shared.cs",
@"namespace Legacy.Core
{
    public class Shared { }
}
", "MyApp", "Other")
                ;

            var report = await RunAsync(solution, solution.PathOf("Common", "Shared.cs"));

            var file = Assert.Single(report.Files);

            Assert.Equal(AdjustFileStatus.Blocked, file.Status);
            Assert.Equal(AdjustBlockKind.CompiledBySeveralProjects, file.Block!.Value.Kind);
        }

        [Fact]
        public async Task A_failure_stops_the_session_and_names_the_file()
        {
            using var solution = OneClassWithConsumer();

            var context = new AdjustContext(
                solution.Workspace,
                new FailingSolutionExplorer(new FakeSolutionExplorer(solution)),
                new TargetNamespaceResolver(solution.Settings, new FakeProjectDefaultNamespaceProvider()),
                new ClosedXamlBodyProviderFactory()
                );

            var report = await new AdjustSession(context, NoRegex())
                .RunAsync(
                    new[]
                    {
                        solution.PathOf("MyApp", @"First\Class1.cs"),
                        solution.PathOf("MyApp", "Consumer.cs")
                    });

            Assert.Equal(AdjustSessionOutcome.Failed, report.Outcome);
            Assert.Contains("the tree is gone", report.FailureMessage);
            Assert.Equal(1, report.FailedCount);
            Assert.Equal(1, report.NotProcessedCount);
            Assert.Equal(AdjustFileStatus.Failed, report.Files[0].Status);
            Assert.Equal(AdjustFileStatus.NotProcessed, report.Files[1].Status);
            Assert.True(report.HasLeftovers);
        }

        [Fact]
        public async Task The_markdown_has_a_section_per_group_of_files()
        {
            using var solution = OneClassWithConsumer();

            var blocked = AdjustBlock.Create(solution.PathOf("MyApp", "Consumer.cs"), AdjustBlockKind.NoProject);

            var report = await new AdjustSession(solution.Context, NoRegex(), new[] { blocked })
                .RunAsync(new[] { solution.PathOf("MyApp", @"First\Class1.cs") });

            var markdown = report.ToMarkdown();

            Assert.StartsWith("# Adjust namespaces report", markdown);
            Assert.Contains("Adjusted 1 file(s), blocked 1, skipped 0, not processed 0.", markdown);
            Assert.Contains("## Adjusted (1)", markdown);
            Assert.Contains(@"### `MyApp\First\Class1.cs`", markdown);
            Assert.Contains("Namespace: `A.B` -> `MyApp.First`", markdown);
            Assert.Contains(@"- `MyApp\Consumer.cs`: using added", markdown);
            Assert.Contains("## Not adjusted", markdown);
            Assert.Contains("### Blocked (1)", markdown);
            Assert.Contains(@"- `MyApp\Consumer.cs`: The file belongs to no project of the solution.", markdown);
            Assert.Contains("## Cleanup (1)", markdown);
            Assert.Contains(@"- `MyApp\Consumer.cs`: using removed by the cleanup", markdown);
        }

        [Fact]
        public async Task The_markdown_of_a_cancelled_run_says_so()
        {
            using var solution = OneClassWithConsumer();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var report = await new AdjustSession(solution.Context, NoRegex())
                .RunAsync(new[] { solution.PathOf("MyApp", @"First\Class1.cs") }, null, cts.Token);

            var markdown = report.ToMarkdown();

            Assert.Contains("The run has been cancelled", markdown);
            Assert.Contains("### Not processed (the run did not get to them) (1)", markdown);
        }

        [Fact]
        public async Task The_json_is_valid_and_carries_the_same_facts()
        {
            using var solution = OneClassWithConsumer();

            var blocked = AdjustBlock.Create(solution.PathOf("MyApp", "Consumer.cs"), AdjustBlockKind.NoProject);

            var report = await new AdjustSession(solution.Context, NoRegex(), new[] { blocked })
                .RunAsync(new[] { solution.PathOf("MyApp", @"First\Class1.cs") });

            var json = JObject.Parse(report.ToJson());

            Assert.Equal("Completed", (string?)json["outcome"]);
            Assert.Equal(1, (int)json["summary"]!["adjusted"]!);
            Assert.Equal(1, (int)json["summary"]!["blocked"]!);

            var files = (JArray)json["files"]!;

            var adjusted = files.Single(f => (string?)f["status"] == "Adjusted");
            Assert.Equal(@"MyApp\First\Class1.cs", (string?)adjusted["path"]);
            Assert.Equal("A.B", (string?)adjusted["namespaces"]![0]!["from"]);
            Assert.Equal("MyApp.First", (string?)adjusted["namespaces"]![0]!["to"]);

            var blockedFile = files.Single(f => (string?)f["status"] == "Blocked");
            Assert.Equal("NoProject", (string?)blockedFile["blockKind"]);

            var cleanup = (JArray)json["cleanupFiles"]!;
            Assert.Equal(1, (int)cleanup[0]["edits"]!["UsingRemoved"]!);
        }

        [Fact]
        public void The_json_escapes_what_has_to_be_escaped()
        {
            var builder = new AdjustReportBuilder(@"C:\sln");
            builder.RecordFile(AdjustFileOutcome.Failed(@"C:\sln\a ""b"".cs", "line1\nline2 \"quoted\" \\ \t"));

            var report = builder.Build(AdjustSessionOutcome.Failed, "boom");

            var json = JObject.Parse(report.ToJson());

            Assert.Equal("boom", (string?)json["failure"]);
            Assert.Equal("a \"b\".cs", (string?)json["files"]![0]!["path"]);
            Assert.Equal("line1\nline2 \"quoted\" \\ \t", (string?)json["files"]![0]!["reason"]);
        }

        [Fact]
        public void A_path_outside_of_the_solution_folder_stays_absolute()
        {
            var builder = new AdjustReportBuilder(@"C:\sln");

            var report = builder.Build(AdjustSessionOutcome.Completed);

            Assert.Equal(@"Sub\a.cs", report.DisplayPath(@"C:\sln\Sub\a.cs"));
            Assert.Equal(@"C:\other\a.cs", report.DisplayPath(@"C:\other\a.cs"));
            Assert.Equal(@"C:\sln2\a.cs", report.DisplayPath(@"C:\sln2\a.cs"));
        }

        [Fact]
        public async Task The_sections_of_the_wizard_follow_the_report()
        {
            using var solution = OneClassWithConsumer();

            var blocked = AdjustBlock.Create(solution.PathOf("MyApp", "Consumer.cs"), AdjustBlockKind.NoProject);

            var report = await new AdjustSession(solution.Context, NoRegex(), new[] { blocked })
                .RunAsync(new[] { solution.PathOf("MyApp", @"First\Class1.cs") });

            var sections = AdjustReportSections.Build(report);

            Assert.Equal(
                new[] { "Adjusted (1)", "Blocked (1)", "Cleanup (1)" },
                sections.Select(s => s.Title).ToArray()
                );

            var adjusted = sections[0].Children.Single();

            Assert.Equal(@"MyApp\First\Class1.cs", adjusted.Title);
            Assert.Equal(solution.PathOf("MyApp", @"First\Class1.cs"), adjusted.FilePath);
            Assert.Contains("A.B -> MyApp.First", adjusted.Detail);
            Assert.Contains(adjusted.Children, c => c.Title == @"MyApp\Consumer.cs" && c.Detail == "using added");

            Assert.Equal("The file belongs to no project of the solution.", sections[1].Children.Single().Detail);
        }

        private static TestSolution OneClassWithConsumer()
        {
            return new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"First\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Class1 Create() => new Class1();
    }
}
")
                ;
        }

        private static async System.Threading.Tasks.Task<AdjustReport> RunAsync(
            TestSolution solution,
            params string[] subjectFilePaths
            )
        {
            return await new AdjustSession(solution.Context, NoRegex())
                .RunAsync(subjectFilePaths);
        }

        private static NamespaceReplaceRegex NoRegex()
        {
            return new NamespaceReplaceRegex(string.Empty, string.Empty);
        }

        /// <summary>
        /// A solution tree which works for the collecting of the xaml files and fails when the
        /// planner asks which project a file belongs to.
        /// </summary>
        private sealed class FailingSolutionExplorer : ISolutionExplorer
        {
            private readonly ISolutionExplorer _inner;

            public FailingSolutionExplorer(ISolutionExplorer inner)
            {
                _inner = inner;
            }

            public System.Threading.Tasks.Task<IReadOnlyList<string>> GetAllFilePathsAsync()
            {
                return _inner.GetAllFilePathsAsync();
            }

            public System.Threading.Tasks.Task<IReadOnlyList<string>> GetTextFilePathsAsync()
            {
                return _inner.GetTextFilePathsAsync();
            }

            public System.Threading.Tasks.Task<IReadOnlyDictionary<string, ProjectRef>> GetProjectOfEveryFileAsync()
            {
                throw new System.InvalidOperationException("the tree is gone");
            }
        }
    }
}
