using AdjustNamespace.Tests.Infrastructure;
using Xunit;

namespace AdjustNamespace.Tests
{
    /// <summary>
    /// Tests of <see cref="NamespaceTypeContainer"/>: the type name conflicts which make
    /// the adjusting impossible are detected with it before the adjusting starts,
    /// see <c>SubjectFileCollector</c>.
    /// </summary>
    public class TypeContainerTests
    {
        [Fact]
        public async Task A_type_of_the_asked_namespace_is_found()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace X.Y
{
    public class Class1 { }
}
")
                ;

            var container = await NamespaceTypeContainer.CreateForAsync(solution.Workspace);

            Assert.True(container.CheckForTypeExists("X.Y", "Class1"));
        }

        [Fact]
        public async Task A_type_of_another_namespace_is_not_found()
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

            var container = await NamespaceTypeContainer.CreateForAsync(solution.Workspace);

            Assert.False(container.CheckForTypeExists("X.Y", "Class1"));
        }

        [Fact]
        public async Task An_unknown_namespace_contains_nothing()
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

            var container = await NamespaceTypeContainer.CreateForAsync(solution.Workspace);

            Assert.False(container.CheckForTypeExists("Nothing.Here", "Class1"));
        }

        /// <summary>
        /// The container covers the whole solution, so a conflict with a type
        /// of another project is detected too: both projects may contribute
        /// the types of the very same namespace.
        /// </summary>
        [Fact]
        public async Task A_type_of_another_project_is_found()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddProject("MyApp.Extras")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp.Extras", "Class2.cs",
@"namespace X.Y
{
    public class Class2 { }
}
")
                ;

            var container = await NamespaceTypeContainer.CreateForAsync(solution.Workspace);

            Assert.True(container.CheckForTypeExists("X.Y", "Class2"));
        }

        [Fact]
        public async Task A_type_of_a_nested_namespace_does_not_belong_to_the_outer_one()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace X.Y.Inner
{
    public class Class1 { }
}
")
                ;

            var container = await NamespaceTypeContainer.CreateForAsync(solution.Workspace);

            Assert.False(container.CheckForTypeExists("X.Y", "Class1"));
        }

        /// <summary>
        /// A nested type is <c>X.Y.Container.Nested</c>, not <c>X.Y.Nested</c>: moving
        /// a type named <c>Nested</c> into <c>X.Y</c> is not a conflict, while the outer
        /// type of the very same declaration is one.
        /// </summary>
        [Fact]
        public async Task A_nested_type_is_not_a_type_of_the_namespace()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Container.cs",
@"namespace X.Y
{
    public class Container
    {
        public class Nested { }
    }
}
")
                ;

            var container = await NamespaceTypeContainer.CreateForAsync(solution.Workspace);

            Assert.False(container.CheckForTypeExists("X.Y", "Nested"));
            Assert.True(container.CheckForTypeExists("X.Y", "Container"));
        }
    }
}
