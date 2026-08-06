using AdjustNamespace.Adjusting.Adjuster.Cs;
using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using Xunit;

namespace AdjustNamespace.Tests.Edit
{
    /// <summary>
    /// Tests of <see cref="RefProcessor"/> — the decision about a single reference.
    ///
    /// The processor changes nothing, it fills an <see cref="EditSet"/>, so these tests read
    /// the decision itself instead of the text it produces (the text is the subject of
    /// <see cref="Adjusting.CsAdjusterReferenceTests"/>).
    /// </summary>
    public class RefProcessorTests
    {
        [Fact]
        public async Task A_short_reference_is_fixed_with_a_using_clause()
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
        public Class1 Value;
    }
}
")
                ;

            var edits = await ProcessRefsAsync(solution, "A.B.Class1", "A.B", "X.Y");

            var edit = Assert.IsType<AddUsingEdit>(
                Assert.Single(edits.EditsOf(solution.PathOf("MyApp", "Consumer.cs")))
                );

            Assert.Equal("X.Y", edit.NamespaceName);
        }

        [Fact]
        public async Task A_qualified_reference_is_rewritten_in_place()
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
        public A.B.Class1 Value;
    }
}
")
                ;

            var edits = await ProcessRefsAsync(solution, "A.B.Class1", "A.B", "X.Y");

            var edit = Assert.IsType<ReplaceTextEdit>(
                Assert.Single(edits.EditsOf(solution.PathOf("MyApp", "Consumer.cs")))
                );

            Assert.Equal("X.Y.Class1", edit.Text);

            var text = solution.TextOf("MyApp", "Consumer.cs");
            Assert.Equal("A.B.Class1", text.Substring(edit.Span.Start, edit.Span.Length));
        }

        /// <summary>
        /// The whole point of the <see cref="EditSet"/>: the analysis decides and changes
        /// nothing, so the decision may be looked at before anything happens.
        /// </summary>
        [Fact]
        public async Task Nothing_is_changed_by_the_processing_itself()
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
        public A.B.Class1 Value;
    }
}
")
                ;

            var before = solution.TextOf("MyApp", "Consumer.cs");

            var edits = await ProcessRefsAsync(solution, "A.B.Class1", "A.B", "X.Y");

            Assert.False(edits.IsEmpty);
            Assert.Equal(before, solution.TextOf("MyApp", "Consumer.cs"));
        }

        /// <summary>
        /// A file which references the moved type ten times needs one using clause and not ten.
        /// </summary>
        [Fact]
        public async Task The_repeated_references_of_a_file_give_a_single_edit()
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
        public Class1 Value1;
        public Class1 Value2;
        public Class1 Value3;
    }
}
")
                ;

            var edits = await ProcessRefsAsync(solution, "A.B.Class1", "A.B", "X.Y");

            Assert.Single(edits.EditsOf(solution.PathOf("MyApp", "Consumer.cs")));
        }

        /// <summary>
        /// The file which declares the type is no subject of the reference processing:
        /// its own namespace declarations are moved instead.
        /// </summary>
        [Fact]
        public async Task The_declaring_file_gets_no_edit()
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
        public Class1 Value;
    }
}
")
                ;

            var edits = await ProcessRefsAsync(solution, "A.B.Class1", "A.B", "X.Y");

            Assert.Empty(edits.EditsOf(solution.PathOf("MyApp", "Class1.cs")));
        }

        /// <summary>
        /// Fill an <see cref="EditSet"/> with the edits of every reference to the given type.
        /// </summary>
        /// <param name="typeFullName">The type which is being moved.</param>
        /// <param name="originalNamespace">The namespace it lives in.</param>
        /// <param name="targetNamespace">The namespace it is being moved into.</param>
        private static async System.Threading.Tasks.Task<EditSet> ProcessRefsAsync(
            TestSolution solution,
            string typeFullName,
            string originalNamespace,
            string targetNamespace
            )
        {
            var edits = new EditSet();

            var refProcessor = new RefProcessor(
                solution.Workspace,
                edits,
                new NamespaceTransition(originalNamespace, targetNamespace, true)
                );

            await refProcessor.ProcessRefsAsync(
                await solution.GetTypeAsync("MyApp", typeFullName)
                );

            return edits;
        }
    }
}
