using AdjustNamespace.Cli;
using Xunit;

namespace AdjustNamespace.Tests.Cli
{
    /// <summary>
    /// Tests of <see cref="PathFilter"/>: the <c>--path</c> options of the console utility.
    /// </summary>
    public class PathFilterTests
    {
        [Fact]
        public void An_empty_filter_matches_every_file()
        {
            var filter = new PathFilter(new string[0]);

            Assert.True(filter.Matches(@"c:\solution\MyApp\Class1.cs"));
            Assert.Empty(filter.UnmatchedRoots);
        }

        [Fact]
        public void A_file_path_matches_itself()
        {
            var filter = new PathFilter(new[] { @"c:\solution\MyApp\Class1.cs" });

            Assert.True(filter.Matches(@"c:\solution\MyApp\Class1.cs"));
            Assert.Empty(filter.UnmatchedRoots);
        }

        [Fact]
        public void A_folder_matches_every_file_under_it()
        {
            var filter = new PathFilter(new[] { @"c:\solution\MyApp" });

            Assert.True(filter.Matches(@"c:\solution\MyApp\Class1.cs"));
            Assert.True(filter.Matches(@"c:\solution\MyApp\Sub\Class1.cs"));
            Assert.Empty(filter.UnmatchedRoots);
        }

        /// <summary>
        /// Same class of bug as <c>TargetNamespaceCalculator.IsSameFolderOrBelow</c>:
        /// a plain prefix match would also accept a sibling folder whose name merely
        /// begins with the root (<c>MyApp.Tests</c> under <c>MyApp</c>).
        /// </summary>
        [Fact]
        public void A_sibling_folder_with_the_same_prefix_does_not_match()
        {
            var filter = new PathFilter(new[] { @"c:\solution\MyApp" });

            Assert.False(filter.Matches(@"c:\solution\MyApp.Tests\Class1.cs"));
            Assert.Single(filter.UnmatchedRoots);
        }

        [Fact]
        public void The_case_of_the_path_does_not_matter()
        {
            var filter = new PathFilter(new[] { @"c:\solution\MyApp" });

            Assert.True(filter.Matches(@"C:\solution\MyApp\Class1.cs"));
            Assert.Empty(filter.UnmatchedRoots);
        }
    }
}
