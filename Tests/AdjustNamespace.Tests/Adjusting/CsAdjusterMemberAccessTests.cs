using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// A fully qualified access to a static member (<c>A.B.Class1.Value</c>) is not a
    /// qualified name but a member access expression, and it is rebuilt from its parts
    /// by <see cref="AdjustNamespace.Adjusting.Adjuster.Cs.RefProcessor"/>
    /// instead of being edited in place. These are the tests of that rebuilding.
    /// </summary>
    public class CsAdjusterMemberAccessTests
    {
        /// <summary>
        /// The expression is rebuilt from the identifiers found in it, and the type
        /// arguments of a generic type are identifiers as well
        /// (<c>Foo</c> of <c>A.B.Class1&lt;Foo&gt;.Value</c>), so they must not be
        /// treated as the parts of the member access chain.
        /// </summary>
        [Fact]
        public async Task A_static_member_of_a_generic_type_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1<T>
    {
        public static int Value => 1;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Foo { }

    public class Consumer
    {
        public int Get() => A.B.Class1<Foo>.Value;
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.Class1<Foo>.Value", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same expression with a predefined type as the type argument: <c>int</c>
        /// is not an identifier, so this case is not affected by the problem above.
        /// </summary>
        [Fact]
        public async Task A_static_member_of_a_generic_type_with_a_predefined_argument_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1<T>
    {
        public static int Value => 1;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public int Get() => A.B.Class1<int>.Value;
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.Class1<int>.Value", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A static member of a nested type: both the outer and the nested type are
        /// moved, so both of them produce a fix for the very same expression.
        /// </summary>
        [Fact]
        public async Task A_static_member_of_a_nested_type_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Outer.cs",
@"namespace A.B
{
    public class Outer
    {
        public class Nested
        {
            public static int Value => 1;
        }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public int Get() => A.B.Outer.Nested.Value;
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Outer.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.Outer.Nested.Value", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The chain behind the type may be longer than a single member.
        /// </summary>
        [Fact]
        public async Task The_whole_chain_behind_the_moved_type_is_kept()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1
    {
        public static string Field = string.Empty;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public int Get() => A.B.Class1.Field.Length;
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.Class1.Field.Length", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// An extension method may be called as an ordinary static method
        /// (<c>A.B.Ext.Twice(x)</c>); the class of such a call is a usual reference.
        /// </summary>
        [Fact]
        public async Task An_extension_method_called_by_the_full_name_is_rewritten()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Ext.cs",
@"namespace A.B
{
    public static class Ext
    {
        public static int Twice(this int value) => value * 2;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public int Get() => A.B.Ext.Twice(21);
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Ext.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.Ext.Twice(21)", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The arguments of the call are not a part of the member access expression
        /// of the moved type and have to stay untouched.
        /// </summary>
        [Fact]
        public async Task The_arguments_of_the_call_are_not_touched()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1
    {
        public static int Sum(int a, int b) => a + b;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public static class Values
    {
        public static int First => 1;
    }

    public class Consumer
    {
        public int Get() => A.B.Class1.Sum(Other.Values.First, 2);
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("X.Y.Class1.Sum(Other.Values.First, 2)", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
