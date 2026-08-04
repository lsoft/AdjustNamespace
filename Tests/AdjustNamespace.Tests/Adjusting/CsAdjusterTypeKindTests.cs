using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The kinds of the declarations a file may contain, and the namespace declarations
    /// which contradict each other.
    /// </summary>
    public class CsAdjusterTypeKindTests
    {
        /// <summary>
        /// A record is a type declaration as any other one.
        /// </summary>
        [Fact]
        public async Task A_record_is_moved_with_its_references()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Point.cs",
@"namespace A.B
{
    public record Point(int X, int Y);
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.Point Create() => new A.B.Point(1, 2);
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Point.cs", "X.Y");

            Assert.Contains("namespace X.Y", solution.TextOf("MyApp", "Point.cs"));
            Assert.Equal(2, CountOf(solution.TextOf("MyApp", "Consumer.cs"), "X.Y.Point"));
        }

        /// <summary>
        /// A struct and a static class are moved as well.
        /// </summary>
        [Fact]
        public async Task A_struct_and_a_static_class_are_moved_with_their_references()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Types.cs",
@"namespace A.B
{
    public struct Size
    {
        public int Width;
    }

    public static class Helper
    {
        public static int Zero => 0;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Size Create() => new Size();

        public int Get() => Helper.Zero;
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Types.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The declaration of the moved type itself may be a generic one.
        /// </summary>
        [Fact]
        public async Task A_generic_type_declaration_is_moved()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1<T>
        where T : class
    {
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Class1<string> Create() => new Class1<string>();
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The using clauses of a file with a file scoped namespace may be written
        /// behind the namespace declaration.
        /// </summary>
        [Fact]
        public async Task A_using_behind_a_file_scoped_namespace_is_processed()
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
@"namespace Other;

using A.B;

public class Consumer
{
    public Class1 Create() => new Class1();
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// One of the namespaces of the file may be the target one already;
        /// the rest of them are moved into it.
        /// </summary>
        [Fact]
        public async Task A_namespace_which_is_the_target_one_already_is_not_touched()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Classes.cs",
@"namespace A.B
{
    public class Class1 { }
}

namespace X.Y
{
    public class Class2 { }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Classes.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Classes.cs");

            Assert.DoesNotContain("namespace A.B", text);
            Assert.Equal(2, CountOf(text, "namespace X.Y"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The very same namespace may be declared in a file both as a nested declaration
        /// and as a flat one. Only the outermost part of a name is replaced with the target
        /// namespace, so <c>A.B</c> becomes <c>X.B</c> in the first case and <c>X</c> in the
        /// second one: one namespace gets two different transitions, and a type is moved by
        /// the transition of the declaration it is written in and not by the one of its
        /// namespace name.
        /// </summary>
        [Fact]
        public async Task The_contradicting_declarations_of_one_namespace_are_processed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Subject.cs",
@"namespace A
{
    namespace B
    {
        public class Inner { }
    }
}

namespace A.B
{
    public class Flat { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Inner CreateInner() => new Inner();

        public Flat CreateFlat() => new Flat();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Subject.cs", "X");

            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
