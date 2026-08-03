using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The using clause of a namespace has to disappear when (and only when) that namespace
    /// has been emptied by the adjusting, see <see cref="AdjustNamespace.Adjusting.Cleanup"/>
    /// and <see cref="AdjustNamespace.Adjusting.NamespaceCenter"/>.
    /// A leftover using of a namespace which does not exist anymore breaks the build,
    /// and so does a removed using of a namespace which is still alive.
    /// </summary>
    public class CleanupTests
    {
        /// <summary>
        /// A using clause may be written with the <c>global::</c> prefix.
        /// </summary>
        [Fact]
        public async Task A_global_qualified_using_of_the_emptied_namespace_is_removed()
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
@"using global::A.B;

namespace Other
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

            Assert.DoesNotContain("A.B", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The name of a using clause may contain any amount of the whitespace between
        /// its parts, and it is still the very same namespace.
        /// </summary>
        [Fact]
        public async Task A_using_written_with_the_spaces_is_removed()
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
@"using A . B;

namespace Other
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

            Assert.DoesNotContain("using A . B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The using clauses may be placed inside the namespace declaration.
        /// </summary>
        [Fact]
        public async Task A_using_inside_a_namespace_declaration_is_removed()
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
@"namespace Other
{
    using A.B;

    public class Consumer
    {
        public Class1 Create() => new Class1();
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("using A.B;", text);
            Assert.Contains("using X.Y;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A namespace whose only type carries the nested ones is emptied as well:
        /// the nested types belong to their outer type and not to the namespace.
        /// </summary>
        [Fact]
        public async Task A_namespace_of_a_type_with_the_nested_types_is_emptied()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Outer.cs",
@"namespace A.B
{
    public class Outer
    {
        public class Nested { }

        public enum Kind { First }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Outer Create() => new Outer();
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Outer.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("using A.B;", text);
            Assert.Contains("using X.Y;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// An enum left in the namespace keeps it alive, hence its using clause has to stay.
        /// </summary>
        [Fact]
        public async Task A_namespace_with_an_enum_left_in_it_keeps_its_using()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Kind.cs",
@"namespace A.B
{
    public enum Kind { First }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Kind Get() => Kind.First;
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A type may be moved into the parent of its own namespace.
        /// </summary>
        [Fact]
        public async Task A_type_moved_into_its_parent_namespace_is_processed()
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
        public Class1 Create() => new Class1();
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "A");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("using A.B;", text);
            Assert.Contains("using A;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A partial class declared in two files is a single type, and the namespace of it
        /// is not emptied while one of its parts is still there.
        /// </summary>
        [Fact]
        public async Task A_namespace_with_a_left_part_of_a_partial_class_keeps_its_using()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Part1.cs",
@"namespace A.B
{
    public partial class Class1
    {
        public int First() => 1;
    }
}
")
                .AddDocument("MyApp", "Part2.cs",
@"namespace A.B
{
    public partial class Class1
    {
        public int Second() => 2;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public int Get() => new Class1().Second();
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Part1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using A.B;", text);
        }

        /// <summary>
        /// A namespace emptied by one file may be filled again by another file of the very
        /// same session (this is what a reorganization of the folders looks like), and its
        /// using clauses have to survive then.
        /// </summary>
        [Fact]
        public async Task A_namespace_refilled_by_another_file_keeps_its_using()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "First.cs",
@"namespace A.B
{
    public class First { }
}
")
                .AddDocument("MyApp", "Second.cs",
@"namespace Other.C
{
    public class Second { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;
using Other.C;

namespace Consumers
{
    public class Consumer
    {
        public First CreateFirst() => new First();

        public Second CreateSecond() => new Second();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            var namespaceCenter = await AdjustAsync(solution, "MyApp", "First.cs", "X.Y");
            await AdjustAsync(solution, namespaceCenter, "MyApp", "Second.cs", "A.B");
            await CleanupAsync(solution, namespaceCenter);

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
