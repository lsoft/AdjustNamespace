using AdjustNamespace.Adjusting;
using AdjustNamespace.Tests.Infrastructure;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Namespace
{
    /// <summary>
    /// Tests of <see cref="NamespaceCenter"/>: it knows which namespaces became empty
    /// during the adjusting, and therefore which using clauses may be removed at the end
    /// of it (see <see cref="Cleanup"/>).
    /// </summary>
    public class NamespaceCenterTests
    {
        [Fact]
        public async Task A_namespace_which_lost_its_only_type_is_removable()
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

            var namespaceCenter = await NamespaceCenter.CreateForAsync(solution.Workspace);
            namespaceCenter.TypeRemoved(await solution.GetTypeAsync("MyApp", "A.B.Class1"));

            Assert.Equal(
                new[] { "A.B" },
                RemovedNamespacesOf(namespaceCenter, "using A.B;", "using Other;")
                );
        }

        [Fact]
        public async Task A_namespace_which_still_has_a_type_is_not_removable()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Classes.cs",
@"namespace A.B
{
    public class Class1 { }

    public class Class2 { }
}
")
                ;

            var namespaceCenter = await NamespaceCenter.CreateForAsync(solution.Workspace);
            namespaceCenter.TypeRemoved(await solution.GetTypeAsync("MyApp", "A.B.Class1"));

            Assert.Empty(RemovedNamespacesOf(namespaceCenter, "using A.B;"));
        }

        [Fact]
        public async Task A_namespace_becomes_removable_when_the_last_of_its_types_has_moved()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Classes.cs",
@"namespace A.B
{
    public class Class1 { }

    public class Class2 { }
}
")
                ;

            var namespaceCenter = await NamespaceCenter.CreateForAsync(solution.Workspace);
            namespaceCenter.TypeRemoved(await solution.GetTypeAsync("MyApp", "A.B.Class1"));
            namespaceCenter.TypeRemoved(await solution.GetTypeAsync("MyApp", "A.B.Class2"));

            Assert.Equal(
                new[] { "A.B" },
                RemovedNamespacesOf(namespaceCenter, "using A.B;")
                );
        }

        /// <summary>
        /// The types of the same namespace may live in different projects; the namespace
        /// is emptied only when all of them have moved.
        /// </summary>
        [Fact]
        public async Task A_namespace_shared_by_two_projects_needs_both_of_them_to_be_emptied()
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
@"namespace A.B
{
    public class Class2 { }
}
")
                ;

            var namespaceCenter = await NamespaceCenter.CreateForAsync(solution.Workspace);
            namespaceCenter.TypeRemoved(await solution.GetTypeAsync("MyApp", "A.B.Class1"));

            Assert.Empty(RemovedNamespacesOf(namespaceCenter, "using A.B;"));

            namespaceCenter.TypeRemoved(await solution.GetTypeAsync("MyApp.Extras", "A.B.Class2"));

            Assert.Equal(
                new[] { "A.B" },
                RemovedNamespacesOf(namespaceCenter, "using A.B;")
                );
        }

        [Fact]
        public async Task Nothing_is_removable_before_the_adjusting()
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

            var namespaceCenter = await NamespaceCenter.CreateForAsync(solution.Workspace);

            Assert.Empty(RemovedNamespacesOf(namespaceCenter, "using A.B;", "using System;"));
        }

        /// <summary>
        /// Ask the namespace center which of the given using clauses may be removed.
        /// </summary>
        private static List<string> RemovedNamespacesOf(
            NamespaceCenter namespaceCenter,
            params string[] usingClauses
            )
        {
            var syntaxRoot = CSharpSyntaxTree
                .ParseText(string.Join(Environment.NewLine, usingClauses))
                .GetRoot()
                ;

            var usingSyntaxes = syntaxRoot
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .ToList()
                ;

            return namespaceCenter
                .GetRemovedNamespaces(usingSyntaxes)
                .ConvertAll(n => ((UsingDirectiveSyntax)n).Name.ToString())
                ;
        }
    }
}
