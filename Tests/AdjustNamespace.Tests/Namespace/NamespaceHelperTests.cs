using AdjustNamespace.Namespace;
using Xunit;

namespace AdjustNamespace.Tests.Namespace
{
    /// <summary>
    /// Tests of <see cref="NamespaceHelper.IsSpecialNamespace"/>.
    /// The special namespaces are never touched by the extension: a codebase may declare
    /// its own attributes in <c>System.*</c> to support the older frameworks.
    /// </summary>
    public class NamespaceHelperTests
    {
        [Theory]
        [InlineData("System")]
        [InlineData("System.Runtime.CompilerServices")]
        [InlineData("System.Diagnostics.CodeAnalysis")]
        [InlineData("Microsoft")]
        [InlineData("Microsoft.CodeAnalysis")]
        public void The_system_and_microsoft_namespaces_are_special(string namespaceName)
        {
            Assert.True(NamespaceHelper.IsSpecialNamespace(namespaceName));
        }

        [Theory]
        [InlineData("MyApp")]
        [InlineData("MyApp.System")]
        [InlineData("MyApp.Microsoft.Helpers")]
        public void A_usual_namespace_is_not_special(string namespaceName)
        {
            Assert.False(NamespaceHelper.IsSpecialNamespace(namespaceName));
        }

        /// <summary>
        /// Only the first part of the name is compared, so a namespace which merely begins
        /// with these words belongs to the user and has to be adjusted as usual.
        /// </summary>
        [Theory]
        [InlineData("SystemX.Utils")]
        [InlineData("Systems.Core")]
        [InlineData("MicrosoftPatterns.Prism")]
        public void A_namespace_which_only_starts_with_these_words_is_not_special(string namespaceName)
        {
            Assert.False(NamespaceHelper.IsSpecialNamespace(namespaceName));
        }

        /// <summary>
        /// A type may be declared outside of any namespace; Roslyn names such a namespace
        /// <c>&lt;global namespace&gt;</c> and an empty name is possible as well.
        /// Neither of them is a special one, and neither of them breaks the check.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("<global namespace>")]
        public void The_global_namespace_is_not_special(string namespaceName)
        {
            Assert.False(NamespaceHelper.IsSpecialNamespace(namespaceName));
        }
    }
}
