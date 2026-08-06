using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.VisualStudio;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of <see cref="AdjustPlanner"/>: the single place which decides whether a file
    /// is a subject to change and what has to happen with it.
    ///
    /// The decision used to be written twice — once for the file list of the second wizard
    /// step and once inside the adjusters, which silently did nothing when they disagreed
    /// with that list. These tests describe the rules of that decision.
    /// </summary>
    public class AdjustPlannerTests
    {
        [Fact]
        public async Task A_file_which_is_going_to_change_is_planned()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Folder1\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                ;

            var plan = await new AdjustPlanner(solution.Context, NoRegex())
                .TryPlanAsync(solution.PathOf("MyApp", @"Folder1\Class1.cs"));

            Assert.NotNull(plan);
            Assert.False(plan!.Value.IsXaml);
            Assert.Equal("MyApp.Folder1", plan.Value.TargetNamespace);
            Assert.Equal(
                new[] { "A.B" },
                plan.Value.Transitions.Transitions.Select(t => t.OriginalName)
                );
            Assert.Equal(
                new[] { "MyApp.Folder1" },
                plan.Value.Transitions.Transitions.Select(t => t.ModifiedName)
                );
        }

        [Fact]
        public async Task A_file_which_is_in_the_target_namespace_already_is_not_planned()
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

            var plan = await new AdjustPlanner(solution.Context, NoRegex())
                .TryPlanAsync(solution.PathOf("MyApp", "Class1.cs"));

            Assert.Null(plan);
        }

        /// <summary>
        /// The wizard works with the list of the files given by the user, and the solution
        /// may have been changed in the meantime.
        /// </summary>
        [Fact]
        public async Task A_file_of_no_project_is_not_planned()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var plan = await new AdjustPlanner(solution.Context, NoRegex())
                .TryPlanAsync(solution.PathOf("MyApp", "NotHere.cs"));

            Assert.Null(plan);
        }

        /// <summary>
        /// A file of a shared project which several projects reference has no target
        /// namespace which suits all of them.
        /// </summary>
        [Fact]
        public async Task A_file_which_several_projects_compile_is_not_planned()
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

            var plan = await AdjustPlanner.TryPlanAsync(
                solution.Workspace,
                solution.PathOf("Common", "Class1.cs"),
                "Common"
                );

            Assert.Null(plan);
        }

        /// <summary>
        /// The target frameworks of a multi target project are several projects of Roslyn
        /// and a single project of the solution, so the rule above must not catch such a file.
        /// </summary>
        [Fact]
        public async Task A_file_of_a_multi_target_project_is_planned()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                ;

            var plan = await AdjustPlanner.TryPlanAsync(
                solution.Workspace,
                solution.PathOf("MyApp", "Class1.cs"),
                "X.Y"
                );

            Assert.NotNull(plan);
        }

        /// <summary>
        /// The namespace the file is moved out of stays alive for one target framework and
        /// becomes empty for another one: whatever is done with the using clauses of it,
        /// one of the target frameworks breaks, so the file is left as it is.
        /// </summary>
        [Fact]
        public async Task A_file_whose_old_namespace_lives_in_another_target_framework_is_not_planned()
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

            var plan = await AdjustPlanner.TryPlanAsync(
                solution.Workspace,
                solution.PathOf("MyApp", "Class1.cs"),
                "X.Y"
                );

            Assert.Null(plan);
        }

        [Fact]
        public async Task A_xaml_file_is_planned()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml",
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            var plan = await new AdjustPlanner(solution.Context, NoRegex())
                .TryPlanAsync(xamlFilePath);

            Assert.NotNull(plan);
            Assert.True(plan!.Value.IsXaml);
            Assert.Equal("MyApp", plan.Value.TargetNamespace);
            Assert.True(plan.Value.Transitions.IsEmpty);
        }

        /// <summary>
        /// The <c>x:Class</c> of a xaml and the namespace of its code behind file are the two
        /// halves of one class: a code behind file which several projects compile is skipped,
        /// so the xaml has to be skipped as well.
        /// </summary>
        [Fact]
        public async Task A_xaml_file_of_a_skipped_code_behind_is_not_planned()
        {
            using var solution = new TestSolution()
                .AddProject("A")
                .AddProject("B")
                .AddSharedProject("Common")
                .AddSharedDocument("Common", "MainWindow.xaml.cs",
@"namespace Legacy.Core
{
    public partial class MainWindow { }
}
", "A", "B")
                ;

            var xamlFilePath = solution.AddXamlFile("Common", "MainWindow.xaml",
@"<Window x:Class=""Legacy.Core.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            var plan = await AdjustPlanner.TryPlanAsync(
                solution.Workspace,
                xamlFilePath,
                "Common"
                );

            Assert.Null(plan);
        }

        /// <summary>
        /// Every namespace declaration of the file gets its own transition, and the file
        /// is planned as soon as there is at least one of them.
        /// </summary>
        [Fact]
        public async Task Every_namespace_of_the_file_gets_a_transition()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Classes.cs",
@"namespace A.B
{
    public class Class1 { }
}

namespace C.D
{
    public class Class2 { }
}
")
                ;

            var plan = await AdjustPlanner.TryPlanAsync(
                solution.Workspace,
                solution.PathOf("MyApp", "Classes.cs"),
                "X.Y"
                );

            Assert.NotNull(plan);
            Assert.Equal(
                new[] { "A.B", "C.D" },
                plan!.Value.Transitions.Transitions.Select(t => t.OriginalName).OrderBy(n => n)
                );
        }

        [Fact]
        public async Task The_replace_regex_is_applied_to_the_target_namespace()
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

            var plan = await new AdjustPlanner(solution.Context, new NamespaceReplaceRegex("^MyApp", "NewRoot"))
                .TryPlanAsync(solution.PathOf("MyApp", "Class1.cs"));

            Assert.NotNull(plan);
            Assert.Equal("NewRoot", plan!.Value.TargetNamespace);
        }

        /// <summary>
        /// The solution tree is expensive to walk and is available from the main thread only,
        /// so a planner asks for it once and answers for all the files of the session out of
        /// that single answer.
        /// </summary>
        [Fact]
        public async Task The_solution_tree_is_walked_once_per_planner()
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
@"namespace A.B
{
    public class Class2 { }
}
")
                ;

            var solutionExplorer = new FakeSolutionExplorer(solution);

            var planner = new AdjustPlanner(
                new AdjustContext(
                    solution.Workspace,
                    solutionExplorer,
                    new TargetNamespaceResolver(
                        solution.Settings,
                        new FakeProjectDefaultNamespaceProvider()
                        ),
                    new NullDocumentOpener()
                    ),
                NoRegex()
                );

            await planner.TryPlanAsync(solution.PathOf("MyApp", "Class1.cs"));
            await planner.TryPlanAsync(solution.PathOf("MyApp", "Class2.cs"));

            Assert.Equal(1, solutionExplorer.GetProjectOfEveryFileCallCount);
        }

        private static NamespaceReplaceRegex NoRegex()
        {
            return new NamespaceReplaceRegex(string.Empty, string.Empty);
        }
    }
}
