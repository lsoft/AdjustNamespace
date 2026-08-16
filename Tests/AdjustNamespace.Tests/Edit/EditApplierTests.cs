using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Adjusting.Edit.Apply;
using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.VisualStudio;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AdjustNamespace.Tests.Edit
{
    /// <summary>
    /// Tests of <see cref="EditApplier"/> over an <c>AdhocWorkspace</c>: an <see cref="EditSet"/>
    /// is written by hand and applied to the solution.
    /// </summary>
    public class EditApplierTests
    {
        [Fact]
        public async Task The_new_using_is_added_after_the_existing_ones()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"using System;
using System.Linq;

namespace MyApp
{
    public class Class1 { }
}
")
                ;

            var edits = new EditSet();
            edits.AddUsing(solution.PathOf("MyApp", "Class1.cs"), "X.Y");

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("using X.Y;", text);
            Assert.True(
                text.IndexOf("using X.Y;", StringComparison.Ordinal) > text.IndexOf("using System.Linq;", StringComparison.Ordinal),
                "the new using has to be placed after the existing ones"
                );
        }

        [Fact]
        public async Task An_already_existing_using_is_not_duplicated()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"using System;
using X.Y;

namespace MyApp
{
    public class Class1 { }
}
")
                ;

            var edits = new EditSet();
            edits.AddUsing(solution.PathOf("MyApp", "Class1.cs"), "X.Y");

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Equal(1, CountOf(text, "using X.Y;"));
        }

        /// <summary>
        /// A using clause may be written with the <c>global::</c> prefix. It already
        /// imports the namespace, so a plain <c>using X.Y;</c> must not be added on top
        /// of it (CS0105). Cleanup normalizes the prefix; the applier has to as well.
        /// </summary>
        [Fact]
        public async Task An_existing_global_using_is_not_duplicated()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"using System;
using global::X.Y;

namespace MyApp
{
    public class Class1 { }
}
")
                ;

            var edits = new EditSet();
            edits.AddUsing(solution.PathOf("MyApp", "Class1.cs"), "X.Y");

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.DoesNotContain("using X.Y;", text);
            Assert.Equal(1, CountOf(text, "using global::X.Y;"));
        }

        [Fact]
        public async Task The_using_is_added_into_a_file_without_any_using()
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

            var edits = new EditSet();
            edits.AddUsing(solution.PathOf("MyApp", "Class1.cs"), "X.Y");

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("using X.Y;", text);
            Assert.True(
                text.IndexOf("using X.Y;", StringComparison.Ordinal) < text.IndexOf("namespace MyApp", StringComparison.Ordinal),
                "the using has to be placed before the namespace clause"
                );
        }

        [Fact]
        public async Task Several_namespaces_are_added_at_once()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"using System;

namespace MyApp
{
    public class Class1 { }
}
")
                ;

            var edits = new EditSet();
            edits.AddUsing(solution.PathOf("MyApp", "Class1.cs"), "X.Y");
            edits.AddUsing(solution.PathOf("MyApp", "Class1.cs"), "Q.W");

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("using X.Y;", text);
            Assert.Contains("using Q.W;", text);
        }

        /// <summary>
        /// <c>using X = A.B;</c> is an alias, it does not import the namespace, so it must not
        /// be taken for an existing import: the reference would stay unresolved otherwise.
        /// </summary>
        [Fact]
        public async Task An_alias_using_is_not_treated_as_an_imported_namespace()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"using System;
using Alias = X.Y;

namespace MyApp
{
    public class Class1 { }
}
")
                ;

            var edits = new EditSet();
            edits.AddUsing(solution.PathOf("MyApp", "Class1.cs"), "X.Y");

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("using X.Y;", text);
        }

        /// <summary>
        /// A <see cref="ReplaceTextEdit"/> addresses its subject by the span it has in the text
        /// of the file, and a new <c>using</c> clause shifts everything behind it: the names
        /// have to be rewritten before the clauses are added, whatever order the edits have
        /// been scheduled in.
        /// </summary>
        [Fact]
        public async Task A_name_is_rewritten_before_the_usings_shift_it()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Consumer.cs",
@"using System;

namespace Other
{
    public class Consumer
    {
        public A.B.Class1 Value;
    }
}
")
                ;

            var filePath = solution.PathOf("MyApp", "Consumer.cs");
            var edits = new EditSet();

            edits.AddUsing(filePath, "Q.W");
            edits.ReplaceText(filePath, SpanOf(solution, "Consumer.cs", "A.B.Class1"), "X.Y.Class1");

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("public X.Y.Class1 Value;", text);
            Assert.Contains("using Q.W;", text);
        }

        /// <summary>
        /// <c>A.B.Outer.Inner</c> is a reference to <c>Outer</c> and a reference to
        /// <c>Inner</c> at once, so one and the same place may be scheduled twice.
        /// The changes of a text must not intersect, and the longest one wins.
        /// </summary>
        [Fact]
        public async Task Two_intersecting_replacements_are_applied_as_the_longest_one()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.Outer.Inner Value;
    }
}
")
                ;

            var filePath = solution.PathOf("MyApp", "Consumer.cs");
            var edits = new EditSet();

            edits.ReplaceText(filePath, SpanOf(solution, "Consumer.cs", "A.B.Outer"), "X.Y.Outer");
            edits.ReplaceText(filePath, SpanOf(solution, "Consumer.cs", "A.B.Outer.Inner"), "X.Y.Outer.Inner");

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("public X.Y.Outer.Inner Value;", text);
        }

        [Fact]
        public async Task A_namespace_declaration_is_renamed()
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

            var edits = new EditSet();
            edits.MoveNamespace(
                solution.PathOf("MyApp", "Class1.cs"),
                new NamespaceTransition("A.B", "X.Y", true)
                );

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("namespace X.Y", text);
            Assert.DoesNotContain("namespace A.B", text);

            //nothing is left in the old namespace, so its using clause would not compile
            Assert.DoesNotContain("using A.B;", text);
        }

        /// <summary>
        /// The moved file may reference the types which stay in its old namespace,
        /// and these references are resolved by the short name only.
        /// </summary>
        [Fact]
        public async Task The_old_namespace_is_imported_when_it_stays_alive()
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

            var edits = new EditSet();
            edits.MoveNamespace(
                solution.PathOf("MyApp", "Class1.cs"),
                new NamespaceTransition("A.B", "X.Y", true)
                );

            await ApplyAsync(solution, edits);

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("using A.B;", text);
            Assert.Contains("namespace X.Y", text);
        }

        private static async Task ApplyAsync(TestSolution solution, EditSet edits)
        {
            await new EditApplier(
                solution.Workspace,
                new NullDocumentOpener(),
                false
                )
                .ApplyAsync(edits);
        }

        /// <summary>
        /// The span of the first occurence of the given text in the given file.
        /// </summary>
        private static TextSpan SpanOf(TestSolution solution, string relativeFilePath, string substring)
        {
            var text = solution.TextOf("MyApp", relativeFilePath);

            var index = text.IndexOf(substring, StringComparison.Ordinal);
            Assert.True(index >= 0, $"'{substring}' is not found in {relativeFilePath}");

            return new TextSpan(index, substring.Length);
        }

        private static int CountOf(string text, string substring)
        {
            var result = 0;
            var index = text.IndexOf(substring, StringComparison.Ordinal);
            while (index >= 0)
            {
                result++;
                index = text.IndexOf(substring, index + substring.Length, StringComparison.Ordinal);
            }

            return result;
        }
    }
}
