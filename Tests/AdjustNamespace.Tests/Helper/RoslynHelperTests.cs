using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Helper
{
    /// <summary>
    /// Tests of <see cref="RoslynHelper"/>: the helpers the reference processing is built of.
    /// </summary>
    public class RoslynHelperTests
    {
        [Fact]
        public void A_name_with_the_global_alias_is_recognized()
        {
            var node = SyntaxFactory.ParseTypeName("global::A.B.Class1");

            Assert.True(node.IsGlobal());
        }

        /// <summary>
        /// A name which merely starts with the word `global` is not a global qualified one.
        /// </summary>
        [Fact]
        public void A_name_of_a_namespace_named_global_is_not_a_global_alias()
        {
            var node = SyntaxFactory.ParseTypeName("globalThings.Class1");

            Assert.False(node.IsGlobal());
        }

        /// <summary>
        /// <see cref="RoslynHelper.ToUpperSyntax{T}"/> returns the outermost node of the
        /// chain of the requested type, i.e. the whole member access expression here.
        /// </summary>
        [Fact]
        public void The_outermost_node_of_a_member_access_chain_is_found()
        {
            var expression = (MemberAccessExpressionSyntax)SyntaxFactory.ParseExpression("A.B.Class1.Value");

            //the innermost access is `A.B`
            var innermost = expression
                .DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Last();

            Assert.Equal("A.B", innermost.ToString());
            Assert.Equal("A.B.Class1.Value", innermost.ToUpperSyntax<MemberAccessExpressionSyntax>()!.ToString());
        }

        /// <summary>
        /// <see cref="RoslynHelper.GoDownTo"/> searches for the exact type of a node.
        /// </summary>
        [Fact]
        public void The_closest_descendant_of_the_asked_type_is_found()
        {
            var root = SyntaxFactory.ParseCompilationUnit(
@"namespace A.B
{
    public class Class1 { }
}
");

            var found = root.GoDownTo(typeof(ClassDeclarationSyntax));

            Assert.NotNull(found);
            Assert.Contains("class Class1", found!.ToString());
        }

        /// <summary>
        /// A type which is not in the tree is not found, and it is not an error.
        /// </summary>
        [Fact]
        public void An_absent_node_type_is_not_found()
        {
            var root = SyntaxFactory.ParseCompilationUnit(
@"namespace A.B
{
    public class Class1 { }
}
");

            Assert.Null(root.GoDownTo(typeof(EnumDeclarationSyntax)));
        }

        /// <summary>
        /// The indentation of a node is its own whitespace only: the comments and the
        /// directives above it belong to it and must not be copied to another node,
        /// see <see cref="RoslynHelper.GetIndentationOf"/>.
        /// </summary>
        [Fact]
        public void The_indentation_of_a_node_does_not_contain_its_comments()
        {
            var root = SyntaxFactory.ParseCompilationUnit(
@"#region Usings

//the framework
    using System;

#endregion
");

            var usingSyntax = root.GetAllDescendants<UsingDirectiveSyntax>()[0];

            Assert.Equal("    ", usingSyntax.GetIndentationOf().ToString());
        }

        /// <summary>
        /// A file without any using clause keeps its header in front of the added one,
        /// see <see cref="RoslynHelper.AddUsingKeepingHeader"/>.
        /// </summary>
        [Fact]
        public void The_header_of_a_file_stays_in_front_of_the_added_using()
        {
            var root = SyntaxFactory.ParseCompilationUnit(
@"//Copyright (c) The Authors

namespace A.B
{
}
");

            var modified = root.AddUsingKeepingHeader(
                SyntaxFactory.UsingDirective(
                    SyntaxFactory.ParseName(" X.Y")
                    ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                );

            Assert.StartsWith("//Copyright (c) The Authors", modified.ToFullString());
            Assert.Contains("using X.Y;", modified.ToFullString());
        }
    }
}
