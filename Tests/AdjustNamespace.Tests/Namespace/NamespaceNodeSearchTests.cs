using AdjustNamespace.Namespace;
using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Namespace
{
    /// <summary>
    /// Tests of <see cref="NamespaceHelper.TryFindNamespaceNodesFor"/>: the search of the
    /// namespace declarations to be renamed by
    /// <see cref="AdjustNamespace.Adjusting.Edit.Apply.MoveNamespaceApplier"/>.
    /// </summary>
    public class NamespaceNodeSearchTests
    {
        [Fact]
        public void A_classic_declaration_is_found()
        {
            Assert.Equal(
                new[] { "A.B" },
                FoundNames(
@"namespace A.B
{
    public class Class1 { }
}",
                    "A.B"
                    )
                );
        }

        [Fact]
        public void A_file_scoped_declaration_is_found()
        {
            Assert.Equal(
                new[] { "A.B" },
                FoundNames(
@"namespace A.B;

public class Class1 { }",
                    "A.B"
                    )
                );
        }

        /// <summary>
        /// The same namespace may be declared several times in a single file;
        /// every declaration has to be found, otherwise a part of the file keeps
        /// the old namespace.
        /// </summary>
        [Fact]
        public void Every_declaration_of_the_same_namespace_is_found()
        {
            Assert.Equal(
                new[] { "A.B", "A.B", "A.B" },
                FoundNames(
@"namespace A.B
{
    public class Class1 { }
}

namespace A.B
{
    public class Class2 { }
}

namespace A.B
{
    public class Class3 { }
}",
                    "A.B"
                    )
                );
        }

        /// <summary>
        /// The name of a nested declaration is the written one (<c>Inner</c>), not the full
        /// one (<c>A.B.Inner</c>): only the outer declaration is renamed, and the nested one
        /// follows it automatically.
        /// </summary>
        [Fact]
        public void A_nested_declaration_is_searched_by_its_written_name()
        {
            const string Code =
@"namespace A.B
{
    namespace Inner
    {
        public class Class1 { }
    }
}";

            Assert.Equal(new[] { "A.B" }, FoundNames(Code, "A.B"));
            Assert.Equal(new[] { "Inner" }, FoundNames(Code, "Inner"));
            Assert.Empty(FoundNames(Code, "A.B.Inner"));
        }

        [Fact]
        public void A_namespace_which_is_not_declared_is_not_found()
        {
            Assert.Empty(
                FoundNames(
@"namespace A.B
{
    public class Class1 { }
}",
                    "Q.W"
                    )
                );
        }

        /// <summary>
        /// A namespace whose name merely starts with the searched one must not be found:
        /// <c>A.BC</c> is not <c>A.B</c>.
        /// </summary>
        [Fact]
        public void A_namespace_with_a_similar_name_is_not_found()
        {
            Assert.Empty(
                FoundNames(
@"namespace A.BC
{
    public class Class1 { }
}",
                    "A.B"
                    )
                );
        }

        private static string[] FoundNames(string code, string namespaceName)
        {
            var syntaxRoot = CSharpSyntaxTree.ParseText(code).GetRoot();

            if (!syntaxRoot.TryFindNamespaceNodesFor(namespaceName, out var found))
            {
                return new string[0];
            }

            return found!.Select(n => n.Name.ToString()).ToArray();
        }
    }
}
