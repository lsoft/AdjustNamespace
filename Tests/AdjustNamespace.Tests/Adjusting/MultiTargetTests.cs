using AdjustNamespace.Adjusting;
using AdjustNamespace.Helper;
using AdjustNamespace.Tests.Infrastructure;
using System.Linq;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// A multi target project (<c>&lt;TargetFrameworks&gt;net48;net8.0&lt;/TargetFrameworks&gt;</c>)
    /// is a single project of the solution and several projects of Roslyn: one per target
    /// framework. Every file of it becomes a document of every one of these projects, exactly
    /// as a file of a shared project does (see <see cref="SharedProjectTests"/>), but this time
    /// the target namespace is well defined, so the file has to be adjusted and not skipped.
    ///
    /// What makes this case its own one is that these projects are not copies of each other:
    /// every one of them defines the conditional compilation symbol of its target framework
    /// and may have its own files (<c>&lt;Compile Condition="'$(TargetFramework)'=='net48'" /&gt;</c>).
    /// The extension takes a single one of these documents
    /// (<c>WorkspaceHelper.GetDocument</c> resolves the current context of the file) and looks
    /// at the solution through it, so everything which exists in another target framework only
    /// is invisible to it.
    /// </summary>
    public class MultiTargetTests
    {
        #region the plain cases

        /// <summary>
        /// The file is a document of every target framework, so every reference to its types
        /// is reported once per target framework. All of these reports point to the same file
        /// and it must be rewritten once.
        /// </summary>
        [Fact]
        public async Task A_moved_type_is_rewritten_once()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace A.Other
{
    public class Consumer
    {
        public A.B.Class1 Create() => new A.B.Class1();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Equal(2, CountOf(text, "X.Y.Class1"));
            Assert.DoesNotContain("A.B", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same through a using clause: the new clause is added once and the old one
        /// is removed, no matter how many target frameworks compile that file.
        /// </summary>
        [Fact]
        public async Task A_using_is_not_duplicated()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace A.Other
{
    public class Consumer
    {
        public Class1 Create() => new Class1();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Equal(1, CountOf(text, "using X.Y;"));
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A type of an ordinary project referenced by a multi target project:
        /// every target framework of it references that project and the reference is found
        /// through every one of them.
        /// </summary>
        [Fact]
        public async Task A_reference_of_a_multi_target_project_to_another_project_is_fixed()
        {
            using var solution = new TestSolution()
                .AddProject("Lib")
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddProjectReference("MyApp", "Lib")
                .AddDocument("Lib", "Class1.cs",
@"namespace Lib.Core
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace MyApp
{
    public class Consumer
    {
        public Lib.Core.Class1 Create() => new Lib.Core.Class1();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Lib", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Equal(2, CountOf(text, "X.Y.Class1"));
            Assert.DoesNotContain("Lib.Core", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A file which belongs to a single target framework of a multi target project
        /// (<c>&lt;Compile Condition="'$(TargetFramework)'=='net48'" /&gt;</c>) belongs
        /// to a single project of Roslyn and is an ordinary file for the extension.
        /// </summary>
        [Fact]
        public async Task A_file_of_a_single_target_framework_is_adjusted()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddMultiTargetDocument("MyApp", "Legacy.cs",
@"namespace A.B
{
    public class Legacy { }
}
", "net48")
                .AddMultiTargetDocument("MyApp", "LegacyConsumer.cs",
@"namespace A.Other
{
    public class LegacyConsumer
    {
        public A.B.Legacy Create() => new A.B.Legacy();
    }
}
", "net48")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Legacy.cs", "X.Y");

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Legacy.cs"));
            Assert.Equal(2, CountOf(solution.TextOf("MyApp", "LegacyConsumer.cs"), "X.Y.Legacy"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The walk through the solution reports every file of it once: the file which all
        /// the target frameworks compile as well as the file which only one of them compiles.
        /// </summary>
        [Fact]
        public void Every_file_is_enumerated_once()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddMultiTargetDocument("MyApp", "Legacy.cs",
@"namespace A.B
{
    public class Legacy { }
}
", "net48")
                ;

            var paths = solution.Workspace.EnumerateAllDocumentFilePaths(
                Predicate.IsProjectInScope,
                Predicate.IsDocumentInScope
                );

            Assert.Equal(
                new[]
                {
                    solution.PathOf("MyApp", "Class1.cs"),
                    solution.PathOf("MyApp", "Legacy.cs"),
                }
                .OrderBy(p => p),
                paths.OrderBy(p => p)
                );
        }

        /// <summary>
        /// A xaml file of a multi target project. Its code behind file is compiled by every
        /// target framework, and the rule which skips a file compiled by several projects
        /// (see <see cref="SharedProjectTests.A_xaml_of_a_shared_project_is_not_adjusted"/>)
        /// must not catch this one.
        /// </summary>
        [Fact]
        public async Task A_xaml_of_a_multi_target_project_is_adjusted()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "MainWindow.xaml.cs",
@"namespace A.B
{
    public partial class MainWindow { }
}
")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml",
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            var adjuster = new XamlAdjuster(solution.Services, false, xamlFilePath, "X.Y");

            Assert.True(await adjuster.IsChangesExistsAsync());
            Assert.True(await adjuster.AdjustAsync());
            Assert.Contains(@"x:Class=""X.Y.MainWindow""", solution.XamlTextOf("MyApp", "MainWindow.xaml"));
        }

        /// <summary>
        /// The check for the name conflicts in the target namespace looks at every project
        /// of the solution, so a type of another target framework is visible for it even
        /// though the adjusting itself sees a single target framework only.
        /// </summary>
        [Fact]
        public async Task A_type_of_another_target_framework_is_visible_for_the_conflict_check()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "Modern.cs",
@"#if NET8_0
namespace X.Y
{
    public class Modern { }
}
#endif
")
                ;

            var container = await NamespaceTypeContainer.CreateForAsync(solution.Workspace);

            Assert.True(container.CheckForTypeExists("X.Y", "Modern"));
        }

        #endregion

        #region the conditional compilation

        /// <summary>
        /// A type which is declared under a conditional compilation symbol exists in one
        /// target framework only, and in the syntax tree of the other one it is a disabled
        /// text, i.e. a trivia and not a declaration. The namespace declaration of the file
        /// is rewritten for all of the target frameworks at once (it is one text), so the
        /// references of all of them have to be rewritten as well: the syntax trees of every
        /// project which compiles the file are processed.
        /// </summary>
        [Fact]
        public async Task A_type_of_every_target_framework_is_moved()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
#if NET48
    public class OldOne { }
#endif
#if NET8_0
    public class NewOne { }
#endif
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace A.Other
{
    public class Consumer
    {
#if NET48
        public A.B.OldOne CreateOld() => new A.B.OldOne();
#endif
#if NET8_0
        public A.B.NewOne CreateNew() => new A.B.NewOne();
#endif
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Class1.cs"));
            Assert.Equal(2, CountOf(text, "X.Y.OldOne"));
            Assert.Equal(2, CountOf(text, "X.Y.NewOne"));
            Assert.DoesNotContain("A.B.", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The type itself is unconditional, but the references to it are guarded.
        /// A reference of the target framework which is not the current context of the file
        /// is found (Roslyn cascades the search to the corresponding symbols of the other
        /// projects) and its location is a span of the syntax tree of that very project,
        /// where that span is a name and not a disabled text: every location is rewritten
        /// against the tree it belongs to.
        /// </summary>
        [Fact]
        public async Task A_reference_under_a_conditional_symbol_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace A.Other
{
    public class Consumer
    {
#if NET48
        public A.B.Class1 CreateOld() => new A.B.Class1();
#endif
#if NET8_0
        public A.B.Class1 CreateNew() => new A.B.Class1();
#endif
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Class1.cs"));
            Assert.Equal(4, CountOf(text, "X.Y.Class1"));
            Assert.DoesNotContain("A.B.", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The namespace the file is moved out of is filled by another file of the project,
        /// but only for one of the target frameworks: the using clause of it is required by
        /// one of them and does not compile for another one, while there is a single text
        /// for both. There is no correct way to move such a file, so it is left as it is,
        /// exactly as a file of a shared project which several projects compile.
        /// </summary>
        [Fact]
        public async Task A_file_whose_old_namespace_lives_in_another_target_framework_is_not_adjusted()
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

            Assert.Empty(await solution.CompilationErrorsAsync());

            var before = solution.TextOf("MyApp", "Class1.cs");

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            Assert.Equal(before, solution.TextOf("MyApp", "Class1.cs"));
            Assert.Contains("using Legacy.Core;", solution.TextOf("MyApp", "Consumer.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same question asked by the other end: the moved file uses a type of its old
        /// namespace, and that type exists in one target framework only, so the moved file
        /// needs the using clause of the old namespace for one target framework and may not
        /// have it for another one. Such a file is left as it is as well.
        /// </summary>
        [Fact]
        public async Task A_file_which_uses_the_old_namespace_of_another_target_framework_is_not_adjusted()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddDocument("MyApp", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1
    {
#if NET8_0
        public Helper Create() => new Helper();
#endif
    }
}
")
                .AddDocument("MyApp", "Helper.cs",
@"#if NET8_0
namespace Legacy.Core
{
    public class Helper { }
}
#endif
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var before = solution.TextOf("MyApp", "Class1.cs");

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            Assert.Equal(before, solution.TextOf("MyApp", "Class1.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        #endregion

        #region a multi target project plus a shared project

        /// <summary>
        /// A shared project referenced by a single multi target project: the file belongs to
        /// as many projects as there are target frameworks, but all of them are the same
        /// project of the solution, so the target namespace is unambiguous and the file is
        /// adjusted.
        /// </summary>
        [Fact]
        public async Task A_shared_file_of_a_single_multi_target_project_is_adjusted()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddSharedProject("Common")
                .AddSharedDocument("Common", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1 { }
}
", "MyApp")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace MyApp
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
            Assert.Equal(2, CountOf(solution.TextOf("MyApp", "Consumer.cs"), "Common.Class1"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A shared project referenced by a multi target project and by an ordinary one:
        /// two different projects of the solution compile that file, so there is no target
        /// namespace for it and it is left as it is.
        /// </summary>
        [Fact]
        public async Task A_shared_file_of_a_multi_target_project_and_of_another_project_is_not_adjusted()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddProject("B")
                .AddSharedProject("Common")
                .AddSharedDocument("Common", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1 { }
}
", "MyApp", "B")
                ;

            var before = solution.TextOf("Common", "Class1.cs");

            await AdjustAndCleanupAsync(solution, "Common", "Class1.cs", "Common");

            Assert.Equal(before, solution.TextOf("Common", "Class1.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same with two multi target projects: four projects of Roslyn, two projects
        /// of the solution, no target namespace.
        /// </summary>
        [Fact]
        public async Task A_shared_file_of_two_multi_target_projects_is_not_adjusted()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("A", "net48", "net8.0")
                .AddMultiTargetProject("B", "net48", "net8.0")
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
        /// A file of a shared project which a single multi target project references is one
        /// file on the disk and one document per target framework: the walk through the
        /// solution reports it once.
        /// </summary>
        [Fact]
        public void A_shared_file_of_a_single_multi_target_project_is_enumerated_once()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddSharedProject("Common")
                .AddSharedDocument("Common", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1 { }
}
", "MyApp")
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
        /// A type of a shared project referenced by the files of the different target
        /// frameworks of its host project. Every one of these files belongs to a single
        /// project of Roslyn, so every reference is found and rewritten in the tree it
        /// belongs to.
        /// </summary>
        [Fact]
        public async Task A_reference_from_every_target_framework_of_the_host_project_is_fixed()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddSharedProject("Common")
                .AddSharedDocument("Common", "Class1.cs",
@"namespace Legacy.Core
{
    public class Class1 { }
}
", "MyApp")
                .AddMultiTargetDocument("MyApp", "OldConsumer.cs",
@"namespace MyApp
{
    public class OldConsumer
    {
        public Legacy.Core.Class1 Create() => new Legacy.Core.Class1();
    }
}
", "net48")
                .AddMultiTargetDocument("MyApp", "NewConsumer.cs",
@"namespace MyApp
{
    public class NewConsumer
    {
        public Legacy.Core.Class1 Create() => new Legacy.Core.Class1();
    }
}
", "net8.0")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Common", "Class1.cs", "Common");

            Assert.Equal(2, CountOf(solution.TextOf("MyApp", "OldConsumer.cs"), "Common.Class1"));
            Assert.Equal(2, CountOf(solution.TextOf("MyApp", "NewConsumer.cs"), "Common.Class1"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A xaml file of a shared project which a single multi target project references:
        /// its code behind file is compiled by several projects of Roslyn and by a single
        /// project of the solution, so it is adjusted as usual.
        /// </summary>
        [Fact]
        public async Task A_xaml_of_a_shared_project_of_a_single_multi_target_project_is_adjusted()
        {
            using var solution = new TestSolution()
                .AddMultiTargetProject("MyApp", "net48", "net8.0")
                .AddSharedProject("Common")
                .AddSharedDocument("Common", "MainWindow.xaml.cs",
@"namespace Legacy.Core
{
    public partial class MainWindow { }
}
", "MyApp")
                ;

            var xamlFilePath = solution.AddXamlFile("Common", "MainWindow.xaml",
@"<Window x:Class=""Legacy.Core.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            var adjuster = new XamlAdjuster(solution.Services, false, xamlFilePath, "Common");

            Assert.True(await adjuster.IsChangesExistsAsync());
            Assert.True(await adjuster.AdjustAsync());
            Assert.Contains(@"x:Class=""Common.MainWindow""", solution.XamlTextOf("Common", "MainWindow.xaml"));
        }

        #endregion
    }
}
