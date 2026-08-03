using AdjustNamespace.Helper;
using AdjustNamespace.Tests.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Helper
{
    /// <summary>
    /// Tests of <see cref="Predicate"/>: the filters which decide what the extension
    /// is able to process. The cleanup walks through the solution with them,
    /// see <c>PerformingViewModel</c>.
    /// </summary>
    public class PredicateTests
    {
        [Fact]
        public void A_csharp_project_is_in_scope()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var project = solution.Workspace.CurrentSolution.Projects.Single();

            Assert.True(project.IsProjectInScope());
        }

        [Fact]
        public void An_absent_project_is_not_in_scope()
        {
            Microsoft.CodeAnalysis.Project? project = null;

            Assert.False(project.IsProjectInScope());
        }

        [Fact]
        public void An_absent_document_is_not_in_scope()
        {
            Document? document = null;

            Assert.False(document.IsDocumentInScope());
        }

        [Fact]
        public void A_document_of_a_csharp_project_is_in_scope()
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

            var document = solution.Workspace.CurrentSolution.Projects.Single().Documents.Single();

            Assert.True(document.IsDocumentInScope());
        }

        /// <summary>
        /// A document which is not on the disk (a generated one, for example) cannot be
        /// processed and is skipped by the whole solution walk.
        /// </summary>
        [Fact]
        public void A_document_without_a_file_path_is_not_in_scope()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var projectId = solution.Workspace.CurrentSolution.Projects.Single().Id;
            var documentId = DocumentId.CreateNewId(projectId);

            Assert.True(
                solution.Workspace.TryApplyChanges(
                    solution.Workspace.CurrentSolution.AddDocument(
                        documentId,
                        "Generated.cs",
                        SourceText.From("namespace A.B { public class Generated { } }")
                        )
                    )
                );

            var document = solution.Workspace.CurrentSolution.GetDocument(documentId)!;

            Assert.Null(document.FilePath);
            Assert.False(document.IsDocumentInScope());

            var paths = solution.Workspace.EnumerateAllDocumentFilePaths(
                Predicate.IsProjectInScope,
                Predicate.IsDocumentInScope
                );

            Assert.Empty(paths);
        }

        /// <summary>
        /// The documents of every project of the solution are enumerated.
        /// </summary>
        [Fact]
        public void Every_document_of_every_project_is_enumerated()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddProject("MyApp.Consumers")
                .AddDocument("MyApp", "Class1.cs", "namespace A.B { public class Class1 { } }")
                .AddDocument("MyApp.Consumers", "Consumer.cs", "namespace C { public class Consumer { } }")
                ;

            var paths = solution.Workspace.EnumerateAllDocumentFilePaths(
                Predicate.IsProjectInScope,
                Predicate.IsDocumentInScope
                );

            Assert.Equal(2, paths.Count);
            Assert.Contains(solution.PathOf("MyApp", "Class1.cs"), paths);
            Assert.Contains(solution.PathOf("MyApp.Consumers", "Consumer.cs"), paths);
        }
    }
}
