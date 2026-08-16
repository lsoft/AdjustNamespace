using AdjustNamespace.Adjusting;
using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.VisualStudio;
using System.Threading.Tasks;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// C# files inside a <c>sqlproj</c>: the extension takes the default namespace from the
    /// project properties (same as for a C# project). The sample
    /// <c>Tests/Standard/DatabaseProject</c> is the manual fixture; these tests cover the
    /// adjusting of such files over an <c>AdhocWorkspace</c>.
    /// </summary>
    public class SqlProjAdjusterTests
    {
        /// <summary>
        /// <c>DatabaseProject.sqlproj</c> declares <c>RootNamespace=DatabaseProject</c>;
        /// the files in the sample start from the wrong namespaces and have to land on
        /// that root plus the folder chain.
        /// </summary>
        [Fact]
        public async Task The_csharp_files_of_a_sqlproj_are_moved_to_the_root_namespace()
        {
            using var solution = new TestSolution()
                .AddProject("DatabaseProject")
                .AddDocument("DatabaseProject", "ClassFile1.cs",
@"namespace FakeDatabaseProject
{
    class ClassFile1
    {
    }
}
")
                .AddDocument("DatabaseProject", @"MyFolder\ClassFile2.cs",
@"namespace FakeDatabaseProject.FakeFolder
{
    class ClassFile2
    {
    }
}
")
                ;

            var rootNamespace = await new FixedProjectDefaultNamespaceProvider("DatabaseProject")
                .GetAsync(new ProjectRef("DatabaseProject", solution.PathOf("DatabaseProject", "ClassFile1.cs")), solution.PathOf("DatabaseProject", "ClassFile1.cs"));

            Assert.Equal("DatabaseProject", rootNamespace);

            var noRegex = new NamespaceReplaceRegex(string.Empty, string.Empty);

            var class1Target = TargetNamespaceCalculator.Compose(
                rootNamespace,
                System.Array.Empty<string>(),
                noRegex
                );
            var class2Target = TargetNamespaceCalculator.Compose(
                rootNamespace,
                new[] { "MyFolder" },
                noRegex
                );

            Assert.Equal("DatabaseProject", class1Target);
            Assert.Equal("DatabaseProject.MyFolder", class2Target);

            await AdjustAsync(solution, "DatabaseProject", "ClassFile1.cs", class1Target);
            await AdjustAsync(solution, "DatabaseProject", @"MyFolder\ClassFile2.cs", class2Target);

            Assert.Contains(
                "namespace DatabaseProject",
                solution.TextOf("DatabaseProject", "ClassFile1.cs")
                );
            Assert.Contains(
                "namespace DatabaseProject.MyFolder",
                solution.TextOf("DatabaseProject", @"MyFolder\ClassFile2.cs")
                );
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        [Fact]
        public async Task A_reference_between_sqlproj_csharp_files_is_fixed()
        {
            using var solution = new TestSolution()
                .AddProject("DatabaseProject")
                .AddDocument("DatabaseProject", "ClassFile1.cs",
@"namespace FakeDatabaseProject
{
    public class ClassFile1
    {
    }
}
")
                .AddDocument("DatabaseProject", @"MyFolder\ClassFile2.cs",
@"namespace FakeDatabaseProject.FakeFolder
{
    public class ClassFile2
    {
        public FakeDatabaseProject.ClassFile1 Other;
    }
}
")
                ;

            await AdjustAsync(solution, "DatabaseProject", "ClassFile1.cs", "DatabaseProject");

            var text = solution.TextOf("DatabaseProject", @"MyFolder\ClassFile2.cs");
            Assert.Contains("DatabaseProject.ClassFile1", text);
            Assert.DoesNotContain("FakeDatabaseProject.ClassFile1", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The default namespace of a sqlproj is the <c>DefaultNamespace</c> / <c>RootNamespace</c>
        /// property, not the fallback which strips the last part of the project name.
        /// </summary>
        private sealed class FixedProjectDefaultNamespaceProvider : IProjectDefaultNamespaceProvider
        {
            private readonly string _rootNamespace;

            public FixedProjectDefaultNamespaceProvider(string rootNamespace)
            {
                _rootNamespace = rootNamespace;
            }

            public Task<string> GetAsync(ProjectRef project, string documentFilePath)
            {
                return Task.FromResult(_rootNamespace);
            }
        }
    }
}
