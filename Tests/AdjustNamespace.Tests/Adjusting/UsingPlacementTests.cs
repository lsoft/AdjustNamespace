using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// A new using clause is inserted behind the using clauses which are in the file already
    /// (see <see cref="AdjustNamespace.Adjusting.Fixer.AddUsingFixer"/>), and the files are
    /// written by the users: the clauses may be placed inside a namespace declaration,
    /// inside a region or may be absent at all.
    /// </summary>
    public class UsingPlacementTests
    {
        /// <summary>
        /// The using clauses inside a namespace declaration are visible in that namespace
        /// only. A new clause added behind the last using of the file lands in the wrong
        /// namespace when the file declares several of them.
        /// </summary>
        [Fact]
        [Trait("Category", "KnownBug")]
        public async Task The_new_using_is_added_into_the_namespace_which_needs_it()
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
@"namespace First
{
    using A.B;

    public class FirstConsumer
    {
        public Class1 Create() => new Class1();
    }
}

namespace Second
{
    using System;

    public class SecondConsumer
    {
        public string Name => string.Empty;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A file without any using clause gets its first one, and the header of such a file
        /// (a license, for example) has to stay where it is and has to stay alone.
        /// </summary>
        [Fact]
        public async Task The_header_of_a_file_without_any_using_survives()
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
@"//Copyright (c) The Authors. All rights reserved.
//Licensed under the MIT license.

namespace A.B.Sub
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

            Assert.Equal(1, CountOf(text, "Copyright"));
            Assert.StartsWith("//Copyright", text);
            Assert.Contains("using X.Y;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A `global using` has to stay in front of the ordinary ones, otherwise the file
        /// does not compile.
        /// </summary>
        [Fact]
        public async Task A_global_using_of_the_file_is_not_broken()
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
@"global using System;

namespace A.B.Sub
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

            Assert.Contains("using X.Y;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The adjusted file always gets the using clause of its own old namespace
        /// (the types left there may be referenced from it), and it must not be added
        /// twice when the file has such a clause already.
        /// </summary>
        [Fact]
        public async Task The_using_of_the_old_namespace_is_not_duplicated()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"using A.B;

namespace A.B
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

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Equal(1, CountOf(text, "using A.B;"));
        }

        /// <summary>
        /// The using clause of the old namespace is not a garbage: the types which stay
        /// in that namespace are referenced from the adjusted file through it.
        /// </summary>
        [Fact]
        public async Task The_adjusted_file_keeps_the_reference_to_its_old_neighbours()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1
    {
        public Class2 Create() => new Class2();
    }
}
")
                .AddDocument("MyApp", "Class2.cs",
@"namespace A.B
{
    public class Class2 { }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("using A.B;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The using clauses may be wrapped into a region.
        /// </summary>
        [Fact]
        public async Task The_region_around_the_using_clauses_is_not_broken()
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
@"#region Usings

using System;

#endregion

namespace A.B.Sub
{
    public class Consumer
    {
        public Class1 Create() => new Class1();
    }
}
")
                ;

            await AdjustAndCleanupAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Equal(1, CountOf(text, "#region Usings"));
            Assert.Equal(1, CountOf(text, "#endregion"));
            Assert.Contains("using X.Y;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
