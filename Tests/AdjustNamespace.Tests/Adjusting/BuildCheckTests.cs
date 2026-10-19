using AdjustNamespace.Adjusting.BuildChecking;
using AdjustNamespace.Adjusting.Report;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.Tests.TextReferences;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of the optional build check: the compilation errors before the run and after it, the
    /// projects it looks at, the new errors in the report and in the Error List.
    /// </summary>
    public class BuildCheckTests
    {
        private static CompilationErrorInfo Error(string project, string id, string message, string? path = null, int line = 1)
        {
            return new CompilationErrorInfo(project, id, message, path, line, 0);
        }

        [Fact]
        public void An_error_that_has_only_moved_is_not_new()
        {
            var before = new[] { Error("A", "CS0103", "no x", @"C:\a.cs", 3) };
            var after = new[] { Error("A", "CS0103", "no x", @"C:\a.cs", 5) };

            var result = BuildChecker.Compare(new[] { "A" }, before, after);

            Assert.Equal(1, result.ErrorsBefore);
            Assert.Equal(1, result.ErrorsAfter);
            Assert.Empty(result.NewErrors);
        }

        [Fact]
        public void An_error_that_was_not_there_is_new_and_a_repeated_one_counts_by_the_number()
        {
            var before = new[] { Error("A", "CS0103", "no x") };
            var after = new[]
            {
                Error("A", "CS0103", "no x", @"C:\a.cs", 1),
                Error("A", "CS0103", "no x", @"C:\b.cs", 2),
                Error("A", "CS0246", "no type", @"C:\b.cs", 7)
            };

            var result = BuildChecker.Compare(new[] { "A" }, before, after);

            Assert.Equal(2, result.NewErrors.Count);
            Assert.Equal(new[] { "CS0103", "CS0246" }, result.NewErrors.Select(e => e.Id).ToArray());
        }

        [Fact]
        public void Only_the_given_projects_are_compared()
        {
            var before = new[] { Error("Other", "CS0001", "old") };
            var after = new[] { Error("Other", "CS0002", "new"), Error("A", "CS0003", "mine") };

            var result = BuildChecker.Compare(new[] { "A" }, before, after);

            Assert.Equal(0, result.ErrorsBefore);
            Assert.Equal(1, result.ErrorsAfter);
            Assert.Equal("CS0003", Assert.Single(result.NewErrors).Id);
        }

        [Fact]
        public async Task The_affected_projects_are_those_of_the_changed_files_and_their_dependents()
        {
            using var solution = new TestSolution()
                .AddProject("Core")
                .AddProject("App")
                .AddProject("Unrelated")
                .AddProjectReference("App", "Core")
                .AddDocument("Core", "A.cs", "namespace Core { public class A { } }")
                .AddDocument("App", "B.cs", "namespace App { public class B { } }")
                .AddDocument("Unrelated", "C.cs", "namespace U { public class C { } }")
                ;

            var configPath = solution.AddTextFile("Unrelated", "app.config", "<a/>");

            Assert.Equal(
                new[] { "App", "Core" },
                BuildChecker.AffectedProjectNames(solution.Workspace, new[] { solution.PathOf("Core", "A.cs") }).ToArray()
                );

            Assert.Equal(
                new[] { "App" },
                BuildChecker.AffectedProjectNames(solution.Workspace, new[] { solution.PathOf("App", "B.cs") }).ToArray()
                );

            //a file that is no document belongs to the project whose folder holds it
            Assert.Equal(
                new[] { "Unrelated" },
                BuildChecker.AffectedProjectNames(solution.Workspace, new[] { configPath }).ToArray()
                );

            Assert.Empty(BuildChecker.AffectedProjectNames(solution.Workspace, new string[0]));
            await System.Threading.Tasks.Task.CompletedTask;
        }

        [Fact]
        public async Task The_errors_of_the_solution_are_captured_with_their_positions()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Broken.cs", "namespace MyApp\n{\n    public class Broken { int x = \"s\"; }\n}\n")
                ;

            var errors = await BuildChecker.CaptureAsync(solution.Workspace);

            var error = Assert.Single(errors);

            Assert.Equal("MyApp", error.ProjectName);
            Assert.Equal("CS0029", error.Id);
            Assert.Equal(solution.PathOf("MyApp", "Broken.cs"), error.FilePath);
            Assert.Equal(3, error.Line);
        }

        [Fact]
        public async Task A_run_with_the_build_check_reports_no_new_errors_and_keeps_the_old_ones_apart()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Class1.cs", "namespace A.B { public class Class1 { } }")
                .AddDocument("MyApp", "Consumer.cs", "using A.B;\nnamespace Other { public class Consumer { Class1 c; int broken = \"s\"; } }")
                ;

            var report = await new AdjustSession(
                    solution.Context,
                    new NamespaceReplaceRegex(string.Empty, string.Empty),
                    runBuildCheck: true
                    )
                .RunAsync(new[] { solution.PathOf("MyApp", @"Models\Class1.cs") });

            var check = report.BuildCheck;

            Assert.NotNull(check);
            Assert.Equal(new[] { "MyApp" }, check!.ProjectNames.ToArray());
            Assert.Equal(1, check.ErrorsBefore);
            Assert.Equal(1, check.ErrorsAfter);
            Assert.Empty(check.NewErrors);
            Assert.False(report.HasLeftovers);
            Assert.Contains("## Build check", report.ToMarkdown());
        }

        /// <summary>
        /// The workspace reads the project file when the solution is opened and never again, so
        /// the moved startup class is still "missing" for it (CS1555) while the real build, which
        /// reads the file the run has changed, is fine. The check compiles with the new name.
        /// </summary>
        [Fact]
        public async Task A_moved_startup_object_is_not_a_new_error()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Program.cs",
@"namespace Legacy.App
{
    public static class Program
    {
        public static void Main()
        {
        }
    }
}
")
                ;

            solution.AddTextFile(
                "MyApp",
                "MyApp.csproj",
                "<Project>\r\n  <PropertyGroup>\r\n    <StartupObject>Legacy.App.Program</StartupObject>\r\n  </PropertyGroup>\r\n</Project>\r\n"
                );

            //what the workspace has read from the project file when it was opened
            var project = solution.Workspace.CurrentSolution.Projects.Single();
            var options = new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.ConsoleApplication)
                .WithMainTypeName("Legacy.App.Program");
            Assert.True(
                solution.Workspace.TryApplyChanges(
                    solution.Workspace.CurrentSolution.WithProjectCompilationOptions(project.Id, options)
                    )
                );

            Assert.Empty(await solution.CompilationErrorsAsync());

            var report = await new AdjustSession(
                    solution.Context,
                    new NamespaceReplaceRegex(string.Empty, string.Empty),
                    textReferenceOptions: AdjustNamespace.TextReferences.TextReferenceOptions.Default,
                    runBuildCheck: true
                    )
                .RunAsync(new[] { solution.PathOf("MyApp", "Program.cs") });

            Assert.Contains(
                "<StartupObject>MyApp.Program<",
                solution.TextFileTextOf("MyApp", "MyApp.csproj")
                );

            Assert.Equal(0, report.BuildCheck!.ErrorsBefore);
            Assert.Equal(0, report.BuildCheck.ErrorsAfter);
            Assert.Empty(report.BuildCheck.NewErrors);
        }

        [Fact]
        public async Task A_run_without_the_build_check_has_none()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Class1.cs", "namespace A.B { public class Class1 { } }")
                ;

            var report = await new AdjustSession(solution.Context, new NamespaceReplaceRegex(string.Empty, string.Empty))
                .RunAsync(new[] { solution.PathOf("MyApp", @"Models\Class1.cs") });

            Assert.Null(report.BuildCheck);
            Assert.DoesNotContain("Build check", report.ToMarkdown());
        }

        [Fact]
        public async Task The_build_check_reports_its_stage()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Models\Class1.cs", "namespace A.B { public class Class1 { } }")
                ;

            var stages = new List<AdjustStage>();

            await new AdjustSession(
                    solution.Context,
                    new NamespaceReplaceRegex(string.Empty, string.Empty),
                    runBuildCheck: true
                    )
                .RunAsync(
                    new[] { solution.PathOf("MyApp", @"Models\Class1.cs") },
                    new SyncProgress(p => stages.Add(p.Stage))
                    );

            Assert.Equal(AdjustStage.BuildCheck, stages.First());
            Assert.Equal(AdjustStage.BuildCheck, stages.Last());
            Assert.Equal(2, stages.Count(s => s == AdjustStage.BuildCheck));
        }

        [Fact]
        public void New_errors_are_in_the_report_the_sections_and_the_error_list()
        {
            var builder = new AdjustReportBuilder(@"C:\sln");
            builder.RecordBuildCheck(
                new BuildCheckResult(
                    new[] { "App" },
                    1,
                    2,
                    new[] { Error("App", "CS0246", "The type 'X' was not found", @"C:\sln\App\B.cs", 7) }
                    )
                );

            var report = builder.Build(AdjustSessionOutcome.Completed);

            var markdown = report.ToMarkdown();
            Assert.Contains("## Build check", markdown);
            Assert.Contains("Compilation errors before the run: 1, after it: 2, new: 1.", markdown);
            Assert.Contains(@"- `App\B.cs:7` CS0246: The type 'X' was not found", markdown);
            Assert.True(report.HasLeftovers);

            var json = Newtonsoft.Json.Linq.JObject.Parse(report.ToJson());
            Assert.Equal(1, (int)json["buildCheck"]!["newErrors"]!.Count());
            Assert.Equal("CS0246", (string?)json["buildCheck"]!["newErrors"]![0]!["id"]);

            var section = AdjustReportSections.Build(report).Single(s => s.Title.StartsWith("Build check"));
            var item = Assert.Single(section.Children);
            Assert.Equal(@"C:\sln\App\B.cs", item.FilePath);
            Assert.Equal(7, item.Line);

            var warning = Assert.Single(AdjustReportErrorList.Build(report));
            Assert.Contains("new compilation error after the run: CS0246", warning.Text);
            Assert.Equal(@"C:\sln\App\B.cs", warning.FilePath);
            Assert.Equal(7, warning.Line);
        }

        [Fact]
        public void The_left_text_references_are_in_the_error_list_and_the_changed_ones_are_not()
        {
            var set = TextReferenceCorpus.Set();
            var matches = TextReferencesTestHelper.Matches(set);

            var builder = new AdjustReportBuilder(@"C:\sln");
            builder.RecordTextReferences(
                new[]
                {
                    new AdjustNamespace.TextReferences.TextReferenceResult(matches[0], true, null),
                    new AdjustNamespace.TextReferences.TextReferenceResult(matches[1], false, "unchecked")
                },
                0
                );

            var items = AdjustReportErrorList.Build(builder.Build(AdjustSessionOutcome.Completed));

            var warning = Assert.Single(items);
            Assert.Contains(matches[1].MatchedText, warning.Text);
            Assert.Contains("unchecked", warning.Text);
            Assert.Equal(matches[1].FilePath, warning.FilePath);
            Assert.Equal(matches[1].Line, warning.Line);
        }

        /// <summary>
        /// A progress sink which reports synchronously.
        /// </summary>
        private sealed class SyncProgress : System.IProgress<AdjustProgress>
        {
            private readonly System.Action<AdjustProgress> _action;

            public SyncProgress(System.Action<AdjustProgress> action)
            {
                _action = action;
            }

            public void Report(AdjustProgress value)
            {
                _action(value);
            }
        }
    }
}
