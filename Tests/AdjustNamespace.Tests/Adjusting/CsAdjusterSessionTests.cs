using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of a whole adjusting session: the user usually selects several files at once,
    /// they are processed one by one within a single <c>NamespaceCenter</c> and the cleanup
    /// runs at the end over the whole solution.
    /// </summary>
    public class CsAdjusterSessionTests
    {
        /// <summary>
        /// The user may run the wizard over an already adjusted file: there is nothing
        /// to change then, and the file must stay byte to byte the same.
        /// </summary>
        [Fact]
        public async Task The_second_run_over_the_same_file_changes_nothing()
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

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var afterFirstRun = solution.TextOf("MyApp", "Class1.cs");

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            Assert.Equal(afterFirstRun, solution.TextOf("MyApp", "Class1.cs"));
        }

        /// <summary>
        /// A file which references the moved type both by a fully qualified name and
        /// through a using clause needs two different fixers at once
        /// (see <c>FixerContainer</c>), and both of them have to be applied.
        /// </summary>
        [Fact]
        public async Task Both_kinds_of_the_references_of_one_file_are_fixed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
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
        public Class1 Short() => new Class1();

        public A.B.Class1 Full() => new A.B.Class1();
    }
}
")
                ;

            var namespaceCenter = await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");
            await CleanupAsync(solution, namespaceCenter);

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("A.B", text);
            Assert.Contains("using X.Y;", text);
            Assert.Equal(2, CountOf(text, "X.Y.Class1"));
        }

        /// <summary>
        /// The types of one namespace may go into different target namespaces, because
        /// the target one is built from the folder of every file separately.
        /// </summary>
        [Fact]
        public async Task Two_files_of_one_namespace_may_go_into_different_namespaces()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"First\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", @"Second\Class2.cs",
@"namespace A.B
{
    public class Class2 { }
}
")
                ;

            var namespaceCenter = await AdjustAsync(solution, "MyApp", @"First\Class1.cs", "MyApp.First");
            await AdjustAsync(solution, namespaceCenter, "MyApp", @"Second\Class2.cs", "MyApp.Second");

            Assert.Contains("namespace MyApp.First", solution.TextOf("MyApp", @"First\Class1.cs"));
            Assert.Contains("namespace MyApp.Second", solution.TextOf("MyApp", @"Second\Class2.cs"));
        }

        /// <summary>
        /// A namespace is emptied by two files together: the using clause of it may be
        /// removed only after both of them have been adjusted.
        /// </summary>
        [Fact]
        public async Task The_using_is_removed_after_the_last_file_of_the_namespace_has_moved()
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
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Class1 Create1() => new Class1();

        public Class2 Create2() => new Class2();
    }
}
")
                ;

            var namespaceCenter = await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");
            await AdjustAsync(solution, namespaceCenter, "MyApp", "Class2.cs", "X.Y");
            await CleanupAsync(solution, namespaceCenter);

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("using A.B;", text);
            Assert.Contains("using X.Y;", text);
        }

        /// <summary>
        /// A type which is used by another type of the very same file needs no using clause:
        /// they move together.
        /// </summary>
        [Fact]
        public async Task The_references_within_the_adjusted_file_itself_are_not_touched()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Classes.cs",
@"namespace A.B
{
    public class Class1 { }

    public class Class2
    {
        public Class1 Create() => new Class1();
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Classes.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Classes.cs");

            Assert.Contains("namespace X.Y", text);
            Assert.Contains("public Class1 Create() => new Class1();", text);
        }

        /// <summary>
        /// A file which is not a part of the solution is skipped silently: the wizard
        /// works with the list of the files given by the user, and the solution may have
        /// been changed in the meantime.
        /// </summary>
        [Fact]
        public async Task An_unknown_file_is_skipped()
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

            var namespaceCenter = await AdjustNamespace.Adjusting.NamespaceCenter.CreateForAsync(solution.Workspace);

            var adjuster = new AdjustNamespace.Adjusting.CsAdjuster(
                solution.Services,
                false,
                namespaceCenter,
                solution.PathOf("MyApp", "NotHere.cs"),
                "X.Y",
                new System.Collections.Generic.List<string>()
                );

            Assert.False(await adjuster.AdjustAsync());
        }

        /// <summary>
        /// The cleanup runs over every document of the solution and may be repeated
        /// (the wizard performs it after every adjusted file): the second run has
        /// nothing left to remove.
        /// </summary>
        [Fact]
        public async Task The_repeated_cleanup_changes_nothing()
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

            var namespaceCenter = await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            await CleanupAsync(solution, namespaceCenter);

            var afterFirstCleanup = solution.TextOf("MyApp", "Class1.cs");

            await CleanupAsync(solution, namespaceCenter);

            Assert.DoesNotContain("using A.B;", afterFirstCleanup);
            Assert.Equal(afterFirstCleanup, solution.TextOf("MyApp", "Class1.cs"));
        }
    }
}
