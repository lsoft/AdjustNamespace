using AdjustNamespace.Adjusting.Fixer;
using AdjustNamespace.Tests.Infrastructure;
using Xunit;

namespace AdjustNamespace.Tests.Fixer
{
    /// <summary>
    /// Tests of <see cref="AddUsingFixer"/> over an <c>AdhocWorkspace</c>.
    /// </summary>
    public class AddUsingFixerTests
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

            await FixAsync(solution, "Class1.cs", "X.Y");

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

            await FixAsync(solution, "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Equal(1, CountOf(text, "using X.Y;"));
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

            await FixAsync(solution, "Class1.cs", "X.Y");

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

            await FixAsync(solution, "Class1.cs", "X.Y", "Q.W");

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

            await FixAsync(solution, "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("using X.Y;", text);
        }

        private static async Task FixAsync(
            TestSolution solution,
            string relativeFilePath,
            params string[] namespacesToAdd
            )
        {
            var fixer = new AddUsingFixer(
                solution.Workspace,
                solution.PathOf("MyApp", relativeFilePath)
                );

            foreach (var namespaceToAdd in namespacesToAdd)
            {
                fixer.AddSubject(namespaceToAdd);
            }

            await fixer.FixAsync();
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
