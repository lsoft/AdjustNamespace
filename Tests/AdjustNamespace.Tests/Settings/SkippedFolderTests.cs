using AdjustNamespace.Settings;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AdjustNamespace.Tests.Settings
{
    /// <summary>
    /// Tests of <see cref="AdjustNamespaceSettings2.IsSkippedFolder"/>: which folders
    /// do not take a part in the target namespace.
    /// Both a rooted path and a path relative to the solution folder are supported,
    /// see the comment in <c>Tests\Standard\adjust_namespaces_settings.xml</c>.
    /// </summary>
    public class SkippedFolderTests
    {
        private const string SolutionFolder = @"c:\solution";

        [Fact]
        public void A_folder_given_by_its_full_path_is_skipped()
        {
            var settings = SettingsWith(@"c:\solution\Project\A\B");

            Assert.True(settings.IsSkippedFolder(@"c:\solution\Project\A\B"));
        }

        [Fact]
        public void A_folder_given_by_a_path_relative_to_the_solution_is_skipped()
        {
            var settings = SettingsWith(@"Project\A\B");

            Assert.True(settings.IsSkippedFolder(@"c:\solution\Project\A\B"));
        }

        [Theory]
        [InlineData(@"c:\solution\Project\A\B\")]
        [InlineData(@"Project\A\B\")]
        public void A_trailing_separator_does_not_matter(string skipped)
        {
            var settings = SettingsWith(skipped);

            Assert.True(settings.IsSkippedFolder(@"c:\solution\Project\A\B"));
        }

        [Fact]
        public void An_unrelated_folder_is_not_skipped()
        {
            var settings = SettingsWith(@"Project\A\B");

            Assert.False(settings.IsSkippedFolder(@"c:\solution\Project\A\C"));
        }

        [Fact]
        public void The_parent_of_a_skipped_folder_is_not_skipped()
        {
            var settings = SettingsWith(@"Project\A\B");

            Assert.False(settings.IsSkippedFolder(@"c:\solution\Project\A"));
        }

        /// <summary>
        /// The comparison is an equality of the full paths, so only the folder itself
        /// is excluded and its subfolders still take a part in the target namespace.
        /// This is the documented behaviour; the test pins it down.
        /// </summary>
        [Fact]
        public void A_subfolder_of_a_skipped_folder_is_not_skipped_itself()
        {
            var settings = SettingsWith(@"Project\A\B");

            Assert.False(settings.IsSkippedFolder(@"c:\solution\Project\A\B\C"));
        }

        /// <summary>
        /// The Windows paths are case insensitive, so a folder written in another case
        /// in the settings file has to match too.
        /// </summary>
        [Fact]
        public void The_case_of_the_path_does_not_matter()
        {
            var settings = SettingsWith(@"Project\A\B");

            Assert.True(settings.IsSkippedFolder(@"c:\solution\project\a\b"));
        }

        /// <summary>
        /// A user may write the path with the unix separators, both a rooted
        /// and a solution-relative one.
        /// </summary>
        [Fact]
        public void A_relative_path_with_the_alt_separators_is_skipped()
        {
            var settings = SettingsWith(@"Project/A/B");

            Assert.True(settings.IsSkippedFolder(@"c:\solution\Project\A\B"));
        }

        [Fact]
        public void A_rooted_path_with_the_alt_separators_is_skipped()
        {
            var settings = SettingsWith(@"c:/solution/Project/A/B");

            Assert.True(settings.IsSkippedFolder(@"c:\solution\Project\A\B"));
        }

        /// <summary>
        /// A path may contain the relative parts; they are resolved before the comparison.
        /// </summary>
        [Fact]
        public void A_rooted_path_with_a_dot_segment_is_skipped()
        {
            var settings = SettingsWith(@"c:\solution\Project\A\..\A\B");

            Assert.True(settings.IsSkippedFolder(@"c:\solution\Project\A\B"));
        }

        [Fact]
        public void An_empty_settings_file_skips_nothing()
        {
            var settings = SettingsWith();

            Assert.False(settings.IsSkippedFolder(@"c:\solution\Project\A\B"));
        }

        private static AdjustNamespaceSettings2 SettingsWith(params string[] skippedFolders)
        {
            return new AdjustNamespaceSettings2(
                SolutionFolder,
                new AdjustNamespaceSettings
                {
                    SkippedFolderSuffixes = new List<string>(skippedFolders)
                }
                );
        }
    }
}
