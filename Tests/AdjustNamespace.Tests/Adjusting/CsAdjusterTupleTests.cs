using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The references written inside a tuple type (<c>(Cat, int) Pair()</c>).
    ///
    /// A nameless tuple element consists of its type only, so the span of the reference is
    /// the span of the whole element — the same tie between a node and its only child a
    /// nameless parameter of a union case list has, see <c>CsAdjusterUnionTests</c>.
    /// </summary>
    public class CsAdjusterTupleTests
    {
        /// <summary>
        /// A moved type referenced by a nameless tuple element gets the using clause.
        /// <c>FindNode</c> answers the outermost node of a tie, which is the tuple element and
        /// not the type name inside it, so <c>RefProcessor</c> descends to the type.
        /// </summary>
        [Fact]
        public async Task A_nameless_tuple_element_of_a_moved_type_gets_a_using()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public (Cat, int) Pair() => default;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Cat.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The same with a named element: the name stands behind the type, so the spans
        /// differ and the type is found as usual.
        /// </summary>
        [Fact]
        public async Task A_named_tuple_element_of_a_moved_type_gets_a_using()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Cat.cs",
@"namespace A.B
{
    public class Cat { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public (Cat Animal, int Count) Pair() => default;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Cat.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
