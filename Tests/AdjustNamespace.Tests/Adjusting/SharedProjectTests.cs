using AdjustNamespace.Adjusting;
using AdjustNamespace.Helper;
using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// A file of a shared project (.shproj) is compiled by every project which references
    /// that shared project: there is one file on the disk and one Roslyn document per
    /// referencing project, and every document belongs to another compilation.
    ///
    /// This breaks the assumptions the extension is built on:
    /// <list type="bullet">
    /// <item>a file belongs to a project, and the namespace of it is derived from that project
    /// (see <c>NamespaceHelper.TryDetermineTargetNamespaceAsync</c>) — for a shared file
    /// there is no such single project, so there is no target namespace which suits
    /// all of them;</item>
    /// <item>a file of the solution is a file of a project, so the walk through the solution
    /// reports such a file once per project which compiles it;</item>
    /// <item>a namespace is either alive or empty after the adjusting — but a namespace
    /// which several projects fill is alive in some of them and empty in the others.</item>
    /// </list>
    ///
    /// The reference search is not a problem: Roslyn cascades it to the corresponding symbols
    /// of the other projects, so the references of every project are found.
    ///
    /// The very same Roslyn picture (one file, several documents) is produced by a multi
    /// target project (<c>net48;net8.0</c>), and that case has to keep working, hence
    /// <see cref="A_file_of_a_multi_target_project_is_adjusted"/>.
    /// </summary>
    public class SharedProjectTests
    {
        #region a file of a shared project as the subject of the adjusting

        /// <summary>
        /// The namespace of a file which several projects compile cannot be adjusted at all:
        /// whatever namespace is chosen, it is derived from one of these projects and does not
        /// match the other ones. Such a file has to be left as it is.
        /// </summary>
        [Fact]
        public async Task A_file_which_several_projects_compile_is_not_adjusted()
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

            var before = solution.TextOf("Common", "Class1.cs");

            await AdjustAndCleanupAsync(solution, "Common", "Class1.cs", "Common");

            Assert.Equal(before, solution.TextOf("Common", "Class1.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A shared project which is referenced by a single project is not ambiguous:
        /// the namespace of its files is well defined and the adjusting works as usual.
        /// A fix of <see cref="A_file_which_several_projects_compile_is_not_adjusted"/>
        /// must not disable this case.
        /// </summary>
        [Fact]
        public async Task A_file_of_a_shared_project_of_a_single_project_is_adjusted()
        {
            using var solution = new TestSolution()
                .AddProject("A")
                .AddSharedProject("Common")
                .AddSharedDocument("Common", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1 { }
}
", "A")
                .AddDocument("A", "Consumer.cs",
@"namespace A
{
    public class Consumer
    {
        public Legacy.Core.Class1 Create() => new Legacy.Core.Class1();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Common", "Class1.cs", "Common");

            Assert.Contains("namespace Common", solution.TextOf("Common", "Class1.cs"));
            Assert.Equal(2, CountOf(solution.TextOf("A", "Consumer.cs"), "Common.Class1"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A file of a multi target project belongs to several Roslyn projects as well,
        /// but all of them are the same project of the solution (the same project file),
        /// so the target namespace is unambiguous and the file is adjusted.
        /// </summary>
        [Fact]
        public async Task A_file_of_a_multi_target_project_is_adjusted()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddMultiTargetDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
", "net48", "net8.0")
                .AddMultiTargetDocument("MyApp", "Consumer.cs",
@"namespace A.Other
{
    public class Consumer
    {
        public A.B.Class1 Create() => new A.B.Class1();
    }
}
", "net48", "net8.0")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Class1.cs"));
            Assert.Equal(2, CountOf(solution.TextOf("MyApp", "Consumer.cs"), "X.Y.Class1"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The type of a shared file is a separate symbol in every project which compiles
        /// that file, and the fully qualified references to it are spread over all of these
        /// projects. Roslyn cascades the reference search to the corresponding symbols
        /// of the other projects, so all of them are found and rewritten.
        /// </summary>
        [Fact]
        public async Task The_solution_compiles_after_a_type_of_a_shared_file_has_been_moved()
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
                .AddDocument("A", "AConsumer.cs",
@"namespace A
{
    public class AConsumer
    {
        public Legacy.Core.Class1 Create() => new Legacy.Core.Class1();
    }
}
")
                .AddDocument("B", "BConsumer.cs",
@"namespace B
{
    public class BConsumer
    {
        public Legacy.Core.Class1 Create() => new Legacy.Core.Class1();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Common", "Class1.cs", "Common");

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same, but the projects reference the type through a using clause: every
        /// project gets the using clause of the target namespace, and the cleanup removes
        /// the clause of the emptied one from all of them.
        /// </summary>
        [Fact]
        public async Task The_using_of_a_shared_namespace_is_fixed_in_every_project()
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
                .AddDocument("A", "AConsumer.cs",
@"using Legacy.Core;

namespace A
{
    public class AConsumer
    {
        public Class1 Create() => new Class1();
    }
}
")
                .AddDocument("B", "BConsumer.cs",
@"using Legacy.Core;

namespace B
{
    public class BConsumer
    {
        public Class1 Create() => new Class1();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Common", "Class1.cs", "Common");

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A type of a shared file referenced from another file of the same shared project:
        /// both of the files are compiled by every project, so both of them are fixed at once.
        /// </summary>
        [Fact]
        public async Task A_reference_inside_the_shared_project_survives_the_move()
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
                .AddSharedDocument("Common", "Consumer.cs",
@"namespace Legacy.Other
{
    public class Consumer
    {
        public Legacy.Core.Class1 Create() => new Legacy.Core.Class1();
    }
}
", "A", "B")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Common", "Class1.cs", "Common");

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        #endregion

        #region a file of a shared project as a consumer of a moved type

        /// <summary>
        /// A type of an ordinary project is referenced by a file of a shared project.
        /// Such a reference is reported once per project which compiles the shared file,
        /// and all of these reports point to the very same file: it must be rewritten once
        /// and not once per project.
        /// </summary>
        [Fact]
        public async Task A_qualified_name_in_a_shared_file_is_rewritten_once()
        {
            using var solution = new TestSolution()
                .AddProject("Lib")
                .AddProject("A")
                .AddProject("B")
                .AddProjectReference("A", "Lib")
                .AddProjectReference("B", "Lib")
                .AddSharedProject("Common")
                .AddDocument("Lib", "Class1.cs",
@"namespace Lib.Core
{
    public class Class1 { }
}
")
                .AddSharedDocument("Common", "Consumer.cs",
@"namespace Common.Core
{
    public class Consumer
    {
        public Lib.Core.Class1 Create() => new Lib.Core.Class1();
    }
}
", "A", "B")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Lib", "Class1.cs", "X.Y");

            var text = solution.TextOf("Common", "Consumer.cs");

            Assert.Equal(2, CountOf(text, "X.Y.Class1"));
            Assert.DoesNotContain("Lib.Core", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same through a using clause: the new using clause is added once
        /// and the old one is removed, no matter how many projects compile that file.
        /// </summary>
        [Fact]
        public async Task A_using_of_a_shared_file_is_not_duplicated()
        {
            using var solution = new TestSolution()
                .AddProject("Lib")
                .AddProject("A")
                .AddProject("B")
                .AddProjectReference("A", "Lib")
                .AddProjectReference("B", "Lib")
                .AddSharedProject("Common")
                .AddDocument("Lib", "Class1.cs",
@"namespace Lib.Core
{
    public class Class1 { }
}
")
                .AddSharedDocument("Common", "Consumer.cs",
@"using Lib.Core;

namespace Common.Core
{
    public class Consumer
    {
        public Class1 Create() => new Class1();
    }
}
", "A", "B")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Lib", "Class1.cs", "X.Y");

            var text = solution.TextOf("Common", "Consumer.cs");

            Assert.Equal(1, CountOf(text, "using X.Y;"));
            Assert.DoesNotContain("using Lib.Core;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        #endregion

        #region the file list of the solution

        /// <summary>
        /// The walk through the solution is a walk through the documents of its projects,
        /// so a file which several projects compile is reported once per project.
        /// The wizard builds its file list of it: such a file is offered to the user
        /// several times and is adjusted several times.
        /// </summary>
        [Fact]
        public void A_file_of_a_shared_project_is_enumerated_once()
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

            var paths = solution.Workspace.EnumerateAllDocumentFilePaths(
                Predicate.IsProjectInScope,
                Predicate.IsDocumentInScope
                );

            Assert.Equal(
                new[] { solution.PathOf("Common", "Class1.cs") },
                paths
                );
        }

        /// <summary>
        /// The same for a file of a multi target project: there is one file on the disk
        /// and one document per target framework.
        /// </summary>
        [Fact]
        public void A_file_of_a_multi_target_project_is_enumerated_once()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddMultiTargetDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
", "net48", "net8.0")
                ;

            var paths = solution.Workspace.EnumerateAllDocumentFilePaths(
                Predicate.IsProjectInScope,
                Predicate.IsDocumentInScope
                );

            Assert.Equal(
                new[] { solution.PathOf("MyApp", "Class1.cs") },
                paths
                );
        }

        #endregion

        #region a namespace which lives in more than one project

        /// <summary>
        /// The adjusted file always gets the using clause of its own old namespace
        /// (see <see cref="AdjustNamespace.Adjusting.Fixer.Specific.NamespaceFixer"/>:
        /// the types which stay there may be referenced from it), and the cleanup removes
        /// that clause again if the namespace has been emptied.
        ///
        /// A namespace is emptied per solution, but a using clause is resolved per project:
        /// a namespace which another project still fills is not empty, and the clause stays
        /// in a file of a project which does not reference that another project at all.
        /// </summary>
        [Fact]
        public async Task The_using_of_the_old_namespace_is_not_added_when_the_project_does_not_have_it()
        {
            using var solution = new TestSolution()
                .AddProject("A")
                .AddProject("B")
                .AddDocument("A", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1 { }
}
")
                //the same namespace in another project, which A does not reference
                .AddDocument("B", "Class2.cs",
@"namespace Legacy.Core
{
    public class Class2 { }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "A", "Class1.cs", "X.Y");

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The very same thing with a shared project: the namespace of the shared file
        /// is filled by one of the projects additionally, so it stays alive for the solution
        /// while one of the projects has nothing in it anymore.
        /// </summary>
        [Fact]
        public async Task A_namespace_of_a_shared_file_which_only_one_project_fills_is_not_broken()
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
                //B fills the very same namespace additionally
                .AddDocument("B", "Class2.cs",
@"namespace Legacy.Core
{
    public class Class2 { }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Common", "Class1.cs", "Common");

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same seen from the other side: a file which referenced the moved type through
        /// the using clause of its namespace keeps that clause, because the namespace is not
        /// empty for the solution. In the project of that file there is nothing in it anymore,
        /// so the clause has to go.
        /// </summary>
        [Fact]
        public async Task The_using_of_a_namespace_which_only_another_project_fills_is_removed()
        {
            using var solution = new TestSolution()
                .AddProject("A")
                .AddProject("B")
                .AddDocument("A", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1 { }
}
")
                .AddDocument("A", "AConsumer.cs",
@"using Legacy.Core;

namespace A
{
    public class AConsumer
    {
        public Class1 Create() => new Class1();
    }
}
")
                //the same namespace in another project, which A does not reference
                .AddDocument("B", "Class2.cs",
@"namespace Legacy.Core
{
    public class Class2 { }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "A", "Class1.cs", "X.Y");

            var text = solution.TextOf("A", "AConsumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using Legacy.Core;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        #endregion

        /// <summary>
        /// A xaml file of a shared project. Its <c>x:Class</c> and the namespace of its code
        /// behind file are the two halves of one class, and the code behind file is skipped
        /// (several projects compile it), so the xaml has to be skipped as well.
        /// </summary>
        [Fact]
        public async Task A_xaml_of_a_shared_project_is_not_adjusted()
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

            const string Body =
@"<Window x:Class=""Legacy.Core.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>";

            var xamlFilePath = solution.AddXamlFile("Common", "MainWindow.xaml", Body);

            var adjuster = new XamlAdjuster(solution.Services, false, xamlFilePath, "Common");

            Assert.False(await adjuster.IsChangesExistsAsync());
            Assert.False(await adjuster.AdjustAsync());
            Assert.Equal(Body, solution.XamlTextOf("Common", "MainWindow.xaml"));
        }

        /// <summary>
        /// The type of a shared file exists in every project which compiles that file,
        /// and the check for the name conflicts in the target namespace has to know it,
        /// see <c>SubjectFileCollector</c>.
        /// </summary>
        [Fact]
        public async Task A_type_of_a_shared_file_is_visible_for_the_conflict_check()
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

            var container = await NamespaceTypeContainer.CreateForAsync(solution.Workspace);

            Assert.True(container.CheckForTypeExists("Legacy.Core", "Class1"));
        }
    }
}
