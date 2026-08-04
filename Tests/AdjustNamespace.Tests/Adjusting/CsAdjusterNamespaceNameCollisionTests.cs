using AdjustNamespace.Adjusting;
using AdjustNamespace.Tests.Infrastructure;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The target namespace ends with the name of the moved type: a short reference
    /// to that type resolves to the namespace and not to the type any more (CS0118),
    /// so it has to be qualified.
    /// </summary>
    public class CsAdjusterNamespaceNameCollisionTests
    {
        [Fact]
        public async Task A_reference_from_the_parent_namespace_survives_the_collision()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Exp12\Class1\Class1.cs",
@"namespace TestProject.Exp12
{
    internal class Class1
    {
    }
}
")
                .AddDocument("MyApp", @"Exp12\Class2.cs",
@"namespace TestProject.Exp12
{
    internal class Class2
    {
        public Class1 MyClass;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustRunner.AdjustAndCleanupAsync(
                solution,
                "MyApp",
                @"Exp12\Class1\Class1.cs",
                "TestProject.Exp12.Class1"
                );

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        [Fact]
        public async Task A_reference_to_a_type_named_as_its_own_namespace_survives()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Exp13\Exp13\Exp13.cs",
@"namespace TestProject.Exp13
{
    internal class Exp13
    {
    }
}
")
                .AddDocument("MyApp", @"Exp13\Class1.cs",
@"namespace TestProject.Exp13
{
    internal class Class1
    {
        Exp13 Field;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustRunner.AdjustAndCleanupAsync(
                solution,
                "MyApp",
                @"Exp13\Exp13\Exp13.cs",
                "TestProject.Exp13.Exp13"
                );

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// <c>Class1.Nested</c>: the head of the name is shadowed, so the whole name
        /// has to be qualified and not only its first part.
        /// </summary>
        [Fact]
        public async Task A_reference_to_a_nested_type_survives_the_collision()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Exp12\Class1\Class1.cs",
@"namespace TestProject.Exp12
{
    internal class Class1
    {
        internal class Nested
        {
        }
    }
}
")
                .AddDocument("MyApp", @"Exp12\Class2.cs",
@"namespace TestProject.Exp12
{
    internal class Class2
    {
        public Class1.Nested MyClass;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustRunner.AdjustAndCleanupAsync(
                solution,
                "MyApp",
                @"Exp12\Class1\Class1.cs",
                "TestProject.Exp12.Class1"
                );

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// <c>Class1.Value</c>: a member access expression whose head is shadowed.
        /// </summary>
        [Fact]
        public async Task A_reference_to_a_static_member_survives_the_collision()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Exp12\Class1\Class1.cs",
@"namespace TestProject.Exp12
{
    internal class Class1
    {
        public static int Value = 1;
    }
}
")
                .AddDocument("MyApp", @"Exp12\Class2.cs",
@"namespace TestProject.Exp12
{
    internal class Class2
    {
        public int Get() => Class1.Value;
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustRunner.AdjustAndCleanupAsync(
                solution,
                "MyApp",
                @"Exp12\Class1\Class1.cs",
                "TestProject.Exp12.Class1"
                );

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// There is no collision when the target namespace does not end with the name of
        /// the type: such a reference gets a using clause as usual and stays short.
        /// </summary>
        [Fact]
        public async Task A_reference_without_a_collision_is_not_qualified()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"Exp12\Class1.cs",
@"namespace TestProject.Exp12
{
    internal class Class1
    {
    }
}
")
                .AddDocument("MyApp", @"Exp12\Class2.cs",
@"namespace TestProject.Exp12
{
    internal class Class2
    {
        public Class1 MyClass;
    }
}
")
                ;

            await AdjustRunner.AdjustAndCleanupAsync(
                solution,
                "MyApp",
                @"Exp12\Class1.cs",
                "TestProject.Exp12.Other"
                );

            var text = solution.TextOf("MyApp", @"Exp12\Class2.cs");

            Assert.Contains("using TestProject.Exp12.Other;", text);
            Assert.Contains("public Class1 MyClass;", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
