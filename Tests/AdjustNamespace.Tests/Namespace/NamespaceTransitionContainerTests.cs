using AdjustNamespace.Namespace;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Namespace
{
    /// <summary>
    /// Tests of <see cref="NamespaceTransitionContainer.GetNamespaceTransitionsFor"/>:
    /// which <c>old namespace -> new namespace</c> pairs are built for a file.
    /// </summary>
    public class NamespaceTransitionContainerTests
    {
        [Fact]
        public void A_classic_namespace_is_replaced_with_the_target_one()
        {
            var ntc = TransitionsFor(
@"namespace A.B
{
    public class Class1 { }
}",
                "X.Y"
                );

            var transition = Assert.Single(ntc.Transitions);
            Assert.Equal("A.B", transition.OriginalName);
            Assert.Equal("X.Y", transition.ModifiedName);
            Assert.True(transition.IsRoot);
        }

        [Fact]
        public void A_file_scoped_namespace_is_replaced_with_the_target_one()
        {
            var ntc = TransitionsFor(
@"namespace A.B;

public class Class1 { }",
                "X.Y"
                );

            var transition = Assert.Single(ntc.Transitions);
            Assert.Equal("A.B", transition.OriginalName);
            Assert.Equal("X.Y", transition.ModifiedName);
            Assert.True(transition.IsRoot);
        }

        [Fact]
        public void A_file_which_is_in_the_target_namespace_already_produces_nothing()
        {
            var ntc = TransitionsFor(
@"namespace X.Y
{
    public class Class1 { }
}",
                "X.Y"
                );

            Assert.True(ntc.IsEmpty);
        }

        [Fact]
        public void Only_the_outermost_part_of_a_nested_namespace_is_replaced()
        {
            var ntc = TransitionsFor(
@"namespace A.B
{
    namespace Inner
    {
        public class Class1 { }
    }
}",
                "X.Y"
                );

            Assert.Equal(2, ntc.Transitions.Count);

            var outer = ntc.Transitions.Single(t => t.OriginalName == "A.B");
            Assert.Equal("X.Y", outer.ModifiedName);
            Assert.True(outer.IsRoot);

            var inner = ntc.Transitions.Single(t => t.OriginalName == "A.B.Inner");
            Assert.Equal("X.Y.Inner", inner.ModifiedName);
            Assert.False(inner.IsRoot);
        }

        [Fact]
        public void Every_namespace_of_a_file_with_several_namespaces_is_processed()
        {
            var ntc = TransitionsFor(
@"namespace A.B
{
    public class Class1 { }
}

namespace Q.W
{
    public class Class2 { }
}",
                "X.Y"
                );

            Assert.Equal(2, ntc.Transitions.Count);
            Assert.Contains(ntc.Transitions, t => t.OriginalName == "A.B");
            Assert.Contains(ntc.Transitions, t => t.OriginalName == "Q.W");
        }

        [Fact]
        public void A_special_namespace_is_not_a_subject_to_change()
        {
            var ntc = TransitionsFor(
@"namespace System.Runtime.CompilerServices
{
    internal sealed class IsExternalInit { }
}",
                "X.Y"
                );

            Assert.True(ntc.IsEmpty);
        }

        /// <summary>
        /// The same namespace may be declared several times in a single file, but it has to be
        /// moved once: the equal transitions are collapsed, so the list of the transitions
        /// and <c>TransitionDict</c> (which is keyed by the original name) agree.
        /// </summary>
        [Fact]
        public void The_repeated_declarations_of_the_same_namespace_produce_a_single_transition()
        {
            var ntc = TransitionsFor(
@"namespace A.B
{
    public class Class1 { }
}

namespace A.B
{
    public class Class2 { }
}",
                "X.Y"
                );

            Assert.Single(ntc.Transitions);
            Assert.Equal(ntc.Transitions.Count, ntc.TransitionDict.Count);
        }

        /// <summary>
        /// A type declared outside of any namespace has no transition at all, so the callers
        /// have to be prepared for it. See also the CsAdjuster tests.
        /// </summary>
        [Fact]
        public void A_file_without_any_namespace_produces_nothing()
        {
            var ntc = TransitionsFor(
@"public class Class1 { }",
                "X.Y"
                );

            Assert.True(ntc.IsEmpty);
        }

        private static NamespaceTransitionContainer TransitionsFor(string code, string targetNamespace)
        {
            var syntaxRoot = CSharpSyntaxTree.ParseText(code).GetRoot();

            return NamespaceTransitionContainer.GetNamespaceTransitionsFor(syntaxRoot, targetNamespace);
        }
    }
}
