using AdjustNamespace.Adjusting;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Tests.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of <see cref="SubjectFileCollector"/>: the second step of the wizard asks it
    /// which of the files chosen by the user are really going to change, and which ones
    /// cannot be adjusted (with a reason).
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

            var results = await CollectResultsAsync(solution, solution.PathOf("MyApp", "Class1.cs"));

            Assert.Single(results.CollectedFiles);
            Assert.Equal(solution.PathOf("MyApp", "Class1.cs"), results.CollectedFiles[0].FilePath);
            Assert.Empty(results.Blocked);
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

            var results = await CollectResultsAsync(solution, solution.PathOf("MyApp", "Class1.cs"));

            Assert.Empty(results.CollectedFiles);
            Assert.Empty(results.Blocked);
        }

        /// <summary>
        /// A file which is no part of the solution tree cannot be adjusted and is reported
        /// as blocked, so the user sees why it is missing from the adjustable list.
        /// </summary>
        [Fact]
        public async Task A_file_of_no_project_is_blocked()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var path = solution.PathOf("MyApp", "NoSuchFile.cs");
            var results = await CollectResultsAsync(solution, path);

            Assert.Empty(results.CollectedFiles);
            var block = Assert.Single(results.Blocked);
            Assert.Equal(path, block.FilePath);
            Assert.Equal(AdjustBlockKind.NoProject, block.Kind);
        }

        /// <summary>
        /// Two subject files may both pass the conflict check against the *current*
        /// solution and still land the same type name into the same target namespace.
        /// The first is collected and reserved; the second is blocked. The scan does
        /// not abort, so other adjustable files of the same run stay available.
        /// </summary>
        [Fact]
        public async Task A_cross_file_type_name_conflict_blocks_only_the_second_file()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\A.cs",
@"namespace Old1
{
    public class Foo { }
}
")
                .AddDocument("MyApp", @"Folder1\B.cs",
@"namespace Old2
{
    public class Foo { }
}
")
                .AddDocument("MyApp", @"Folder1\Ok.cs",
@"namespace Old3
{
    public class Ok { }
}
")
                ;

            var results = await CollectResultsAsync(
                solution,
                solution.PathOf("MyApp", @"Folder1\A.cs"),
                solution.PathOf("MyApp", @"Folder1\B.cs"),
                solution.PathOf("MyApp", @"Folder1\Ok.cs")
                );

            Assert.Equal(2, results.CollectedFiles.Count);
            Assert.Contains(results.CollectedFiles, f => f.FilePath == solution.PathOf("MyApp", @"Folder1\A.cs"));
            Assert.Contains(results.CollectedFiles, f => f.FilePath == solution.PathOf("MyApp", @"Folder1\Ok.cs"));

            var block = Assert.Single(results.Blocked);
            Assert.Equal(solution.PathOf("MyApp", @"Folder1\B.cs"), block.FilePath);
            Assert.Equal(AdjustBlockKind.TypeNameConflict, block.Kind);
            Assert.Contains("already contains a type 'Foo'", block.Message);
        }

        /// <summary>
        /// Moving the type into a namespace which declares a type of the same name already
        /// would break the solution, so that file is blocked before anything has been changed.
        /// Other adjustable files of the same scan are still collected.
        /// </summary>
        [Fact]
        public async Task A_type_name_conflict_in_the_target_namespace_is_blocked()
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
                .AddDocument("MyApp", @"Folder1\Other.cs",
@"namespace Legacy
{
    public class Other { }
}
")
                ;

            var results = await CollectResultsAsync(
                solution,
                solution.PathOf("MyApp", @"Folder1\Class1.cs"),
                solution.PathOf("MyApp", @"Folder1\Other.cs")
                );

            Assert.Single(results.CollectedFiles);
            Assert.Equal(solution.PathOf("MyApp", @"Folder1\Other.cs"), results.CollectedFiles[0].FilePath);

            var block = Assert.Single(results.Blocked);
            Assert.Equal(solution.PathOf("MyApp", @"Folder1\Class1.cs"), block.FilePath);
            Assert.Equal(AdjustBlockKind.TypeNameConflict, block.Kind);
            Assert.Contains("already contains a type 'Class1'", block.Message);
            Assert.Contains("MyApp.Folder1", block.Message);
        }

        /// <summary>
        /// An enum is a type just like a class: moving it onto an existing type of the
        /// same name breaks the solution (CS0101), so the conflict has to be reported
        /// before anything has been changed. <see cref="CsAdjuster"/> moves enums, and
        /// the collector must not let them through unchecked.
        /// </summary>
        [Fact]
        public async Task An_enum_name_conflict_in_the_target_namespace_is_blocked()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Kind.cs",
@"namespace A.B
{
    public enum Kind { First }
}
")
                .AddDocument("MyApp", @"Folder1\Occupant.cs",
@"namespace MyApp.Folder1
{
    public class Kind { }
}
")
                ;

            var results = await CollectResultsAsync(solution, solution.PathOf("MyApp", @"Folder1\Kind.cs"));

            Assert.Empty(results.CollectedFiles);
            var block = Assert.Single(results.Blocked);
            Assert.Equal(solution.PathOf("MyApp", @"Folder1\Kind.cs"), block.FilePath);
            Assert.Contains("already contains a type 'Kind'", block.Message);
        }

        /// <summary>
        /// A delegate is a type just like a class: same rule as for enums.
        /// </summary>
        [Fact]
        public async Task A_delegate_name_conflict_in_the_target_namespace_is_blocked()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Handler.cs",
@"namespace A.B
{
    public delegate void Handler(int value);
}
")
                .AddDocument("MyApp", @"Folder1\Occupant.cs",
@"namespace MyApp.Folder1
{
    public class Handler { }
}
")
                ;

            var results = await CollectResultsAsync(solution, solution.PathOf("MyApp", @"Folder1\Handler.cs"));

            Assert.Empty(results.CollectedFiles);
            var block = Assert.Single(results.Blocked);
            Assert.Equal(solution.PathOf("MyApp", @"Folder1\Handler.cs"), block.FilePath);
            Assert.Contains("already contains a type 'Handler'", block.Message);
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

            var results = await CollectResultsAsync(solution, solution.PathOf("MyApp", @"Folder1\Class1.cs"));

            Assert.Single(results.CollectedFiles);
            Assert.Empty(results.Blocked);
        }

        /// <summary>
        /// A file which several projects compile has no single target namespace,
        /// so it is blocked with a reason and not offered as adjustable.
        /// </summary>
        [Fact]
        public async Task A_file_which_several_projects_compile_is_blocked()
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

            var path = solution.PathOf("Common", "Class1.cs");
            var results = await CollectResultsAsync(solution, path);

            Assert.Empty(results.CollectedFiles);
            var block = Assert.Single(results.Blocked);
            Assert.Equal(path, block.FilePath);
            Assert.Equal(AdjustBlockKind.CompiledBySeveralProjects, block.Kind);
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

            var results = await CollectResultsAsync(solution, xamlFilePath);

            Assert.Single(results.CollectedFiles);
            Assert.Equal(xamlFilePath, results.CollectedFiles[0].FilePath);
            Assert.Empty(results.Blocked);
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

            var results = await CollectResultsAsync(solution, xamlFilePath);

            Assert.Empty(results.CollectedFiles);
            Assert.Empty(results.Blocked);
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
            Assert.Empty(results.Blocked);
        }

        /// <summary>
        /// A linked file outside of the project folder cannot get a target namespace.
        /// </summary>
        [Fact]
        public async Task A_linked_file_outside_of_the_project_folder_is_blocked()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var linkedPath = solution.AddLinkedDocument(
                "MyApp",
                @"Outside\Class1.cs",
@"namespace Legacy
{
    public class Class1 { }
}
"
                );

            var results = await CollectResultsAsync(solution, linkedPath);

            Assert.Empty(results.CollectedFiles);
            var block = Assert.Single(results.Blocked);
            Assert.Equal(linkedPath, block.FilePath);
            Assert.Equal(AdjustBlockKind.TargetNamespaceUnknown, block.Kind);
        }

        /// <summary>
        /// The target frameworks of a multi target project may disagree whether the old
        /// namespace stays alive; such a file is blocked and not adjusted.
        /// </summary>
        [Fact]
        public async Task A_file_with_a_contradictory_namespace_state_is_blocked()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Extra.cs",
@"#if NET8_0
namespace Legacy.Core
{
    public class Extra { }
}
#endif
")
                .AddDocument("MyApp", "Consumer.cs",
@"using Legacy.Core;

namespace MyApp
{
    public class Consumer
    {
#if NET8_0
        public Extra Create() => new Extra();
#endif
    }
}
")
                ;

            var path = solution.PathOf("MyApp", "Class1.cs");
            var results = await CollectResultsAsync(solution, path);

            Assert.Empty(results.CollectedFiles);
            var block = Assert.Single(results.Blocked);
            Assert.Equal(path, block.FilePath);
            Assert.Equal(AdjustBlockKind.NamespaceStateContradictory, block.Kind);
        }

        /// <summary>
        /// The compile-after-adjust invariant: a solution which compiles stays compiling
        /// when only the collected (non-blocked) files are adjusted. A type-name conflict
        /// is left alone instead of producing CS0101.
        /// </summary>
        [Fact]
        public async Task Adjusting_only_the_collected_files_keeps_the_solution_compiling()
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
                .AddDocument("MyApp", @"Folder1\Other.cs",
@"namespace Legacy
{
    public class Other { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace MyApp
{
    public class Consumer
    {
        public A.B.Class1 Create1() => new A.B.Class1();

        public MyApp.Folder1.Class1 CreateOccupant() => new MyApp.Folder1.Class1();

        public Legacy.Other CreateOther() => new Legacy.Other();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var results = await CollectResultsAsync(
                solution,
                solution.PathOf("MyApp", @"Folder1\Class1.cs"),
                solution.PathOf("MyApp", @"Folder1\Other.cs")
                );

            Assert.Single(results.CollectedFiles);
            Assert.Equal(solution.PathOf("MyApp", @"Folder1\Other.cs"), results.CollectedFiles[0].FilePath);
            Assert.Single(results.Blocked);

            var outcome = await new AdjustNamespace.Adjusting.Session.AdjustSession(
                solution.Context,
                new NamespaceReplaceRegex(string.Empty, string.Empty)
                ).RunAsync(
                    results.CollectedFiles.ConvertAll(f => f.FilePath),
                    null,
                    default
                    );

            Assert.Equal(AdjustNamespace.Adjusting.Session.AdjustSessionOutcome.Completed, outcome);
            Assert.Contains("namespace A.B", solution.TextOf("MyApp", @"Folder1\Class1.cs"));
            Assert.Contains("namespace MyApp.Folder1", solution.TextOf("MyApp", @"Folder1\Other.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// C# 11: a file-local type (<c>file class Helper</c>) is visible in its own file only,
        /// so two of them of the same name in one namespace are no conflict for the compiler,
        /// and neither is a file-local type next to an ordinary one of the same name.
        /// The file must not be blocked for that.
        /// </summary>
        [Fact]
        public async Task A_file_local_type_of_the_same_name_is_no_conflict()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Existing.cs",
@"namespace MyApp.Folder1
{
    file class Helper { }

    public class Existing { }
}
")
                .AddDocument("MyApp", @"Folder1\Moved.cs",
@"namespace Old
{
    file class Helper { }

    public class Moved { }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var results = await CollectResultsAsync(solution, solution.PathOf("MyApp", @"Folder1\Moved.cs"));

            Assert.Empty(results.Blocked);
            Assert.Single(results.CollectedFiles);
        }

        /// <summary>
        /// The same for an ordinary type which moves next to a file-local type of the same
        /// name, see <c>CsAdjusterModernSyntaxTests.An_ordinary_type_moves_next_to_a_file_local_one_of_the_same_name</c>.
        /// </summary>
        [Fact]
        public async Task An_ordinary_type_next_to_a_file_local_one_of_the_same_name_is_no_conflict()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Existing.cs",
@"namespace MyApp.Folder1
{
    file class Helper { }

    public class Existing
    {
        public object Make() => new Helper();
    }
}
")
                .AddDocument("MyApp", @"Folder1\Helper.cs",
@"namespace Old
{
    public class Helper { }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var results = await CollectResultsAsync(solution, solution.PathOf("MyApp", @"Folder1\Helper.cs"));

            Assert.Empty(results.Blocked);
            Assert.Single(results.CollectedFiles);
        }

        /// <summary>
        /// A file-local type is no conflict, but an ordinary type of the same name still is:
        /// the check must not lose the real conflicts while it skips the file-local types.
        /// </summary>
        [Fact]
        public async Task An_ordinary_type_of_the_same_name_is_still_a_conflict_next_to_a_file_local_one()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Existing.cs",
@"namespace MyApp.Folder1
{
    public class Helper { }
}
")
                .AddDocument("MyApp", @"Folder1\Moved.cs",
@"namespace Old
{
    file class Secret { }

    public class Helper { }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var results = await CollectResultsAsync(solution, solution.PathOf("MyApp", @"Folder1\Moved.cs"));

            var block = Assert.Single(results.Blocked);
            Assert.Equal(AdjustBlockKind.TypeNameConflict, block.Kind);
        }

        /// <summary>
        /// Both parts of a partial class are chosen, but they lie in different folders and
        /// would land in different namespaces, i.e. become two different classes: both files
        /// are blocked (see <see cref="AdjustBlockKind.PartialTypeSplit"/>).
        /// </summary>
        [Fact]
        public async Task The_parts_of_a_partial_class_which_would_land_in_different_namespaces_are_blocked()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Foo.cs",
@"namespace Old
{
    public partial class Foo { }
}
")
                .AddDocument("MyApp", @"Folder2\Foo.Part.cs",
@"namespace Old
{
    public partial class Foo
    {
        public int Value;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var results = await CollectResultsAsync(
                solution,
                solution.PathOf("MyApp", @"Folder1\Foo.cs"),
                solution.PathOf("MyApp", @"Folder2\Foo.Part.cs")
                );

            Assert.Empty(results.CollectedFiles);
            Assert.Equal(2, results.Blocked.Count);
            Assert.All(results.Blocked, b => Assert.Equal(AdjustBlockKind.PartialTypeSplit, b.Kind));
        }

        private const string ProjectFileGlobalUsings =
@"// <auto-generated/>
global using global::A.B;
global using global::System;
";

        /// <summary>
        /// A namespace imported by the project file (<c>&lt;Using Include="A.B" /&gt;</c>, see
        /// <see cref="AdjustBlockKind.NamespaceImportedByProjectFile"/>) must not become empty:
        /// the single file which empties it is blocked with that reason.
        /// </summary>
        [Fact]
        public async Task A_file_which_empties_a_namespace_imported_by_the_project_file_is_blocked()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", @"obj\Debug\net8.0\MyApp.GlobalUsings.g.cs", ProjectFileGlobalUsings)
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var results = await CollectResultsAsync(solution, solution.PathOf("MyApp", @"Folder1\Cat.cs"));

            Assert.Empty(results.CollectedFiles);
            var block = Assert.Single(results.Blocked);
            Assert.Equal(AdjustBlockKind.NamespaceImportedByProjectFile, block.Kind);
            Assert.Contains("'A.B'", block.Message);
            Assert.Contains("'MyApp'", block.Message);
        }

        /// <summary>
        /// Two files empty such a namespace only together: each of them alone leaves the other
        /// one in it, so the planner passes both, and the collector blocks both.
        /// </summary>
        [Fact]
        public async Task Files_which_empty_a_namespace_imported_by_the_project_file_together_are_blocked()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", @"Folder1\Dog.cs",
@"namespace A.B
{
    public class Dog { }
}
")
                .AddDocument("MyApp", @"Folder1\Ok.cs",
@"namespace Old
{
    public class Ok { }
}
")
                .AddDocument("MyApp", @"obj\Debug\net8.0\MyApp.GlobalUsings.g.cs", ProjectFileGlobalUsings)
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var results = await CollectResultsAsync(
                solution,
                solution.PathOf("MyApp", @"Folder1\Cat.cs"),
                solution.PathOf("MyApp", @"Folder1\Dog.cs"),
                solution.PathOf("MyApp", @"Folder1\Ok.cs")
                );

            var collected = Assert.Single(results.CollectedFiles);
            Assert.Equal(solution.PathOf("MyApp", @"Folder1\Ok.cs"), collected.FilePath);

            Assert.Equal(2, results.Blocked.Count);
            Assert.All(results.Blocked, b => Assert.Equal(AdjustBlockKind.NamespaceImportedByProjectFile, b.Kind));
        }

        /// <summary>
        /// The namespace keeps a type of a file which is not moved, and a child namespace
        /// keeps a namespace alive as well: nothing is blocked.
        /// </summary>
        [Fact]
        public async Task A_namespace_imported_by_the_project_file_which_stays_alive_blocks_nothing()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", @"Folder1\Dog.cs",
@"namespace A.B
{
    public class Dog { }
}
")
                .AddDocument("MyApp", @"Other\Horse.cs",
@"namespace A.B.Stable
{
    public class Horse { }
}
")
                .AddDocument("MyApp", @"obj\Debug\net8.0\MyApp.GlobalUsings.g.cs", ProjectFileGlobalUsings)
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var results = await CollectResultsAsync(
                solution,
                solution.PathOf("MyApp", @"Folder1\Cat.cs"),
                solution.PathOf("MyApp", @"Folder1\Dog.cs")
                );

            Assert.Empty(results.Blocked);
            Assert.Equal(2, results.CollectedFiles.Count);
        }

        private static async System.Threading.Tasks.Task<SubjectFileCollector.SubjectCollectingResults> CollectResultsAsync(
            TestSolution solution,
            params string[] subjectFilePaths
            )
        {
            var collector = new SubjectFileCollector(
                solution.Context,
                new HashSet<string>(subjectFilePaths),
                new NamespaceReplaceRegex(string.Empty, string.Empty)
                );

            return await collector.AnalyzeAndCollectAsync((i, total, filePath) => { });
        }
    }
}
