using AdjustNamespace.Adjusting;
using AdjustNamespace.Tests.Infrastructure;
using System.Collections.Generic;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of <see cref="SubjectFileCollector"/>: the second step of the wizard asks it
    /// which of the files chosen by the user are really going to change, and it reports
    /// the type name conflicts which would make the adjusting impossible.
    /// </summary>
    public class SubjectFileCollectorTests
    {
        [Fact]
        public async Task A_file_which_is_going_to_change_is_collected()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                ;

            var collected = await CollectAsync(solution, solution.PathOf("MyApp", "Class1.cs"));

            Assert.Single(collected);
            Assert.Equal(solution.PathOf("MyApp", "Class1.cs"), collected[0].FilePath);
        }

        /// <summary>
        /// The user is not bothered with the files which are in the right namespace already.
        /// </summary>
        [Fact]
        public async Task A_file_which_is_in_the_target_namespace_already_is_not_collected()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace MyApp
{
    public class Class1 { }
}
")
                ;

            var collected = await CollectAsync(solution, solution.PathOf("MyApp", "Class1.cs"));

            Assert.Empty(collected);
        }

        /// <summary>
        /// A file which is no part of the solution tree is silently ignored.
        /// </summary>
        [Fact]
        public async Task A_file_of_no_project_is_not_collected()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var collected = await CollectAsync(solution, solution.PathOf("MyApp", "NoSuchFile.cs"));

            Assert.Empty(collected);
        }

        /// <summary>
        /// The whole point of this step: moving the type into a namespace which declares
        /// a type of the same name already would break the solution, so it is reported
        /// before anything has been changed.
        /// </summary>
        [Fact]
        public async Task A_type_name_conflict_in_the_target_namespace_is_reported()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", @"Folder1\Occupant.cs",
@"namespace MyApp.Folder1
{
    public class Class1 { }
}
")
                ;

            var excp = await Assert.ThrowsAsync<FileProcessException>(
                () => CollectAsync(solution, solution.PathOf("MyApp", @"Folder1\Class1.cs"))
                );

            Assert.Equal(solution.PathOf("MyApp", @"Folder1\Class1.cs"), excp.FilePath);
            Assert.Contains("already contains a type 'Class1'", excp.Message);
        }

        /// <summary>
        /// A nested type moves together with its outer type and never conflicts
        /// with a type of the target namespace.
        /// </summary>
        [Fact]
        public async Task A_nested_type_of_the_same_name_is_no_conflict()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Class1.cs",
@"namespace A.B
{
    public class Class1
    {
        public class Occupant { }
    }
}
")
                .AddDocument("MyApp", @"Folder1\Occupant.cs",
@"namespace MyApp.Folder1
{
    public class Occupant { }
}
")
                ;

            var collected = await CollectAsync(solution, solution.PathOf("MyApp", @"Folder1\Class1.cs"));

            Assert.Single(collected);
        }

        /// <summary>
        /// A file which several projects compile has no single target namespace,
        /// so it is not offered to the user at all.
        /// </summary>
        [Fact]
        public async Task A_file_which_several_projects_compile_is_not_collected()
        {
            using var solution = new TestSolution()
                .AddProject("A")
                .AddProject("B")
                .AddSharedProject("Common")
                .AddSharedDocument("Common", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1 { }
}
", "A", "B")
                ;

            var collected = await CollectAsync(solution, solution.PathOf("Common", "Class1.cs"));

            Assert.Empty(collected);
        }

        [Fact]
        public async Task A_xaml_file_which_is_going_to_change_is_collected()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml",
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            var collected = await CollectAsync(solution, xamlFilePath);

            Assert.Single(collected);
            Assert.Equal(xamlFilePath, collected[0].FilePath);
        }

        [Fact]
        public async Task A_xaml_file_which_is_in_the_target_namespace_already_is_not_collected()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml",
@"<Window x:Class=""MyApp.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            var collected = await CollectAsync(solution, xamlFilePath);

            Assert.Empty(collected);
        }

        /// <summary>
        /// The progress of the scan is reported for every incoming file and not for
        /// the collected ones only.
        /// </summary>
        [Fact]
        public async Task Every_incoming_file_is_reported_to_the_progress_callback()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Class2.cs",
@"namespace MyApp
{
    public class Class2 { }
}
")
                ;

            var reported = new List<string>();

            var collector = new SubjectFileCollector(
                solution.Context,
                new HashSet<string>
                {
                    solution.PathOf("MyApp", "Class1.cs"),
                    solution.PathOf("MyApp", "Class2.cs"),
                },
                new NamespaceReplaceRegex(string.Empty, string.Empty)
                );

            await collector.AnalyzeAndCollectAsync((i, total, filePath) => reported.Add(filePath));

            Assert.Equal(2, reported.Count);
            Assert.Contains(solution.PathOf("MyApp", "Class1.cs"), reported);
            Assert.Contains(solution.PathOf("MyApp", "Class2.cs"), reported);
        }

        /// <summary>
        /// The regex of the settings is applied to the target namespace the files
        /// are compared against.
        /// </summary>
        [Fact]
        public async Task The_replace_regex_is_taken_into_account()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace NewRoot
{
    public class Class1 { }
}
")
                ;

            var collector = new SubjectFileCollector(
                solution.Context,
                new HashSet<string> { solution.PathOf("MyApp", "Class1.cs") },
                new NamespaceReplaceRegex("^MyApp", "NewRoot")
                );

            var results = await collector.AnalyzeAndCollectAsync((i, total, filePath) => { });

            Assert.Empty(results.CollectedFiles);
        }

        private static async System.Threading.Tasks.Task<List<UI.FileEx>> CollectAsync(
            TestSolution solution,
            params string[] subjectFilePaths
            )
        {
            var collector = new SubjectFileCollector(
                solution.Context,
                new HashSet<string>(subjectFilePaths),
                new NamespaceReplaceRegex(string.Empty, string.Empty)
                );

            var results = await collector.AnalyzeAndCollectAsync((i, total, filePath) => { });

            return results.CollectedFiles;
        }
    }
}
