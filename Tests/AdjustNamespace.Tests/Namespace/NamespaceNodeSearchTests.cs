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
        /// The API takes the full name of the namespace. A nested declaration is found
        /// by that full name and not by the written fragment alone, otherwise a root
        /// rename of <c>A</c> would also rewrite a nested <c>namespace A</c> inside
        /// another wrapper (and turn <c>Wrapping.A</c> into <c>Wrapping.MyApp</c>).
        /// </summary>
        [Fact]
        public void A_nested_declaration_is_searched_by_its_full_name()
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
            Assert.Equal(new[] { "Inner" }, FoundNames(Code, "A.B.Inner"));
            Assert.Empty(FoundNames(Code, "Inner"));
        }

        /// <summary>
        /// A nested declaration whose written name equals a root one elsewhere in the
        /// same file must not be found when the root is searched: only the root's full
        /// name matches.
        /// </summary>
        [Fact]
        public void A_nested_declaration_with_the_same_written_name_is_not_a_root()
        {
            const string Code =
@"namespace Wrapping
{
    namespace A
    {
        public class Nested { }
    }
}

namespace A
{
    public class Subject { }
}";

            Assert.Equal(new[] { "A" }, FoundNames(Code, "A"));
            Assert.Equal(new[] { "A" }, FoundNames(Code, "Wrapping.A"));
            Assert.Equal(new[] { "Wrapping" }, FoundNames(Code, "Wrapping"));
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
