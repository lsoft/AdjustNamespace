using AdjustNamespace.Adjusting;
using AdjustNamespace.Tests.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of <see cref="CsAdjuster"/>: the whole processing of a C# file
    /// (its own namespace clauses plus every reference to its types across the solution),
    /// performed over an <c>AdhocWorkspace</c> instead of the Visual Studio one.
    /// </summary>
    public class CsAdjusterTests
    {
        [Fact]
        public async Task The_namespace_of_the_adjusted_file_is_changed()
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

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("namespace X.Y", text);
            Assert.DoesNotContain("namespace A.B", text);
        }

        [Fact]
        public async Task A_file_scoped_namespace_is_changed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B;

public class Class1 { }
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("namespace X.Y;", text);
            Assert.DoesNotContain("namespace A.B;", text);
        }

        [Fact]
        public async Task A_reference_from_another_file_gets_a_using()
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

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
        }

        [Fact]
        public async Task A_reference_from_another_project_gets_a_using()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddProject("MyApp.Consumers")
                .AddProjectReference("MyApp.Consumers", "MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp.Consumers", "Consumer.cs",
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

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp.Consumers", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
        }

        [Fact]
        public async Task A_fully_qualified_reference_is_rewritten()
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
        public A.B.Class1 Create() => new A.B.Class1();
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("A.B.Class1", text);
            Assert.Contains("X.Y.Class1", text);
        }

        [Fact]
        public async Task A_reference_to_a_nested_type_is_processed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1
    {
        public class Nested { }
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"namespace Other
{
    public class Consumer
    {
        public A.B.Class1.Nested Create() => new A.B.Class1.Nested();
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("A.B.Class1", text);
        }

        [Fact]
        public async Task A_usage_of_an_extension_method_is_processed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Extensions.cs",
@"namespace A.B
{
    public static class Extensions
    {
        public static string Twice(this string s) => s + s;
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public string Do() => ""a"".Twice();
    }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Extensions.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
        }

        [Fact]
        public async Task The_emptied_namespace_is_removed_from_the_usings()
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

            var namespaceCenter = await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");
            await CleanupAsync(solution, namespaceCenter);

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
        }

        [Fact]
        public async Task The_adjusted_file_does_not_keep_the_using_of_its_own_old_namespace()
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

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.DoesNotContain("using A.B;", text);
        }

        [Fact]
        public async Task A_reference_in_a_xaml_file_is_updated()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "MyButton.cs",
@"namespace A.B
{
    public class MyButton { }
}
")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml",
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton />
</Window>");

            await AdjustAsync(solution, "MyApp", "MyButton.cs", "X.Y", xamlFilePath);

            var xaml = solution.XamlTextOf("MyApp", "MainWindow.xaml");

            Assert.Contains("clr-namespace:X.Y", xaml);
        }

        /// <summary>
        /// A type declared outside of any namespace has no transition at all: it is simply
        /// left alone, and the rest of the file is adjusted as usual.
        /// </summary>
        [Fact]
        public async Task A_type_in_the_global_namespace_does_not_break_the_adjusting()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"public class GlobalClass { }

namespace A.B
{
    public class Class1 { }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.Contains("namespace X.Y", text);
            Assert.Contains("public class GlobalClass", text);
        }

        /// <summary>
        /// The same namespace may be declared twice in a single file. It produces a single
        /// transition (see the NamespaceTransitionContainer tests), and
        /// <see cref="AdjustNamespace.Adjusting.Edit.Apply.MoveNamespaceApplier"/> has to fix
        /// every declaration of that namespace within that single transition.
        /// </summary>
        [Fact]
        public async Task Both_declarations_of_a_repeated_namespace_are_changed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}

namespace A.B
{
    public class Class2 { }
}
")
                ;

            await AdjustAsync(solution, "MyApp", "Class1.cs", "X.Y");

            var text = solution.TextOf("MyApp", "Class1.cs");

            Assert.DoesNotContain("namespace A.B", text);
            Assert.Equal(2, CountOf(text, "namespace X.Y"));
        }

    }
}
