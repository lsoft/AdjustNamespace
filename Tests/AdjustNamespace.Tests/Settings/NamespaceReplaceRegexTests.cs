using Xunit;

namespace AdjustNamespace.Tests.Settings
{
    /// <summary>
    /// Tests of <see cref="NamespaceReplaceRegex"/>: the user defined regex which additionally
    /// modifies the target namespace determined from the file location.
    /// </summary>
    public class NamespaceReplaceRegexTests
    {
        [Fact]
        public void The_first_part_of_the_namespace_is_renamed()
        {
            //this is one of the samples offered by the wizard
            var regex = new NamespaceReplaceRegex("^[^.]+", "NewRoot");

            Assert.Equal("NewRoot.Folder1.Folder2", regex.Modify("OldRoot.Folder1.Folder2"));
        }

        [Fact]
        public void A_namespace_which_does_not_match_is_not_changed()
        {
            var regex = new NamespaceReplaceRegex("Nothing", "Something");

            Assert.Equal("A.B.C", regex.Modify("A.B.C"));
        }

        [Fact]
        public void An_empty_regex_disables_the_modification()
        {
            var regex = new NamespaceReplaceRegex("", "NewRoot");

            Assert.Equal("A.B.C", regex.Modify("A.B.C"));
        }

        [Fact]
        public void A_captured_group_may_be_used_in_the_replacement()
        {
            var regex = new NamespaceReplaceRegex(@"^Old\.(.+)$", "New.$1");

            Assert.Equal("New.Folder1.Folder2", regex.Modify("Old.Folder1.Folder2"));
        }

        [Fact]
        public void Every_occurrence_is_replaced()
        {
            var regex = new NamespaceReplaceRegex("Old", "New");

            Assert.Equal("New.Middle.New", regex.Modify("Old.Middle.Old"));
        }

        /// <summary>
        /// The regex is typed by the user in the wizard, so it may be an incomplete one
        /// while they are typing it. It must not break the scanning of the solution:
        /// the wizard catches <c>FileProcessException</c> only, and the exception of
        /// <c>Regex</c> escapes the whole operation.
        /// </summary>
        [Fact]
        public void An_invalid_regex_disables_the_modification()
        {
            var regex = new NamespaceReplaceRegex("[unclosed", "NewRoot");

            Assert.Equal("A.B.C", regex.Modify("A.B.C"));
        }

        /// <summary>
        /// An empty replacement disables the modification instead of removing the found
        /// fragment, so a part of a namespace cannot be cut off with this regex.
        /// This is the current behaviour; the test pins it down.
        /// </summary>
        [Fact]
        public void An_empty_replacement_disables_the_modification()
        {
            var regex = new NamespaceReplaceRegex(@"\.Folder1", "");

            Assert.Equal("Root.Folder1.Folder2", regex.Modify("Root.Folder1.Folder2"));
        }
    }
}
