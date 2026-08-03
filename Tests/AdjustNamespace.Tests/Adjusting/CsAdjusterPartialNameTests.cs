using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// A reference to a type is not always written either as a bare name or as a full one:
    /// <c>using A;</c> plus <c>B.Class1</c> is a partially qualified name, and so is
    /// <c>B.Class1</c> written inside the namespace <c>A.Other</c>.
    /// Such a name is a qualified one for
    /// <see cref="AdjustNamespace.Adjusting.Adjuster.Cs.RefProcessor"/>, hence these tests.
    /// </summary>
    public class CsAdjusterPartialNameTests
    {
        /// <summary>
        /// An alias of the parent namespace (<c>using L = A;</c>) makes <c>L.B.Class1</c>
        /// a partially qualified name: the namespace part of it is <c>L.B</c>.
        /// </summary>
        [Fact]
        public async Task A_partially_qualified_name_behind_a_namespace_alias_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Root.cs",
@"namespace A
{
    public class Root { }
}
")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using L = A;

namespace Other
{
    public class Consumer
    {
        public L.B.Class1 Create() => new L.B.Class1();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("L.B.Class1", text);
            Assert.Equal(2, CountOf(text, "X.Y.Class1"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A partially qualified name needs no using clause at all when the file itself
        /// is placed in a namespace the name may be resolved from
        /// (<c>B.Class1</c> inside <c>A.Other</c> is <c>A.B.Class1</c>).
        /// </summary>
        [Fact]
        public async Task A_partially_qualified_name_resolved_by_the_own_namespace_is_rewritten()
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
@"namespace A.Other
{
    public class Consumer
    {
        public B.Class1 Create() => new B.Class1();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Equal(2, CountOf(text, "X.Y.Class1"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same partially qualified name, but in a member access expression
        /// (<c>B.Class1.StaticMethod()</c>).
        /// </summary>
        [Fact]
        public async Task A_partially_qualified_access_to_a_static_member_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1
    {
        public static int Value => 1;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace A.Other
{
    public class Consumer
    {
        public int Get() => B.Class1.Value;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.Class1.Value", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The rewritten name is a relative one and is resolved from the namespace of the
        /// file it is written in. If that namespace contains the first part of the target
        /// namespace (<c>Some.X</c> vs the target <c>X.Y</c>), the rewritten name points
        /// to <c>Some.X.Y</c> and the file does not compile anymore; only a <c>global::</c>
        /// prefix makes such a name unambiguous.
        /// </summary>
        [Fact]
        [Trait("Category", "KnownBug")]
        public async Task A_rewritten_name_is_resolved_unambiguously()
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
@"namespace Some.X
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

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// An alias of the old namespace (<c>using L = A.B;</c>) is a namespace alias,
        /// so <c>L.Class1</c> is a qualified name as well.
        /// </summary>
        [Fact]
        public async Task A_reference_through_a_namespace_alias_is_rewritten()
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
@"using L = A.B;

namespace Other
{
    public class Consumer
    {
        public L.Class1 Create() => new L.Class1();
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Equal(2, CountOf(text, "X.Y.Class1"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A nested type referenced through the using clause of its namespace
        /// (<c>using A.B;</c> + <c>Outer.Nested</c>). <c>Outer.Nested</c> contains no
        /// namespace at all, so the name itself stays as it is and a new using clause
        /// is the only possible fix.
        /// </summary>
        [Fact]
        public async Task A_nested_type_referenced_through_a_using_gets_the_new_using()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Outer.cs",
@"namespace A.B
{
    public class Outer
    {
        public class Nested { }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Outer.Nested Create() => new Outer.Nested();
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Outer.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Contains("Outer.Nested", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// <c>typeof</c> and <c>nameof</c> are the places where a type name is written
        /// as an expression and not as a type; both of them have to be processed.
        /// </summary>
        [Fact]
        public async Task The_references_inside_typeof_and_nameof_are_rewritten()
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
    public class Consumer
    {
        public string Name => nameof(A.B.Class1);

        public System.Type Type => typeof(A.B.Class1);
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("nameof(X.Y.Class1)", text);
            Assert.Contains("typeof(X.Y.Class1)", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
