using AdjustNamespace.Namespace;
using AdjustNamespace.Settings;
using System.Collections.Generic;
using Xunit;

namespace AdjustNamespace.Tests.Namespace
{
    /// <summary>
    /// Tests of <see cref="TargetNamespaceCalculator"/>: the namespace a file has to live in,
    /// derived from its location.
    ///
    /// The rule itself used to be a part of <c>NamespaceHelper.TryDetermineTargetNamespaceAsync</c>,
    /// which asks DTE for the default namespace of the project in the middle of the computation
    /// and therefore could only be tested by the manual procedure. The pure part of it is
    /// separated now, and the default namespace comes in as a parameter, see
    /// <see cref="AdjustNamespace.VisualStudio.IProjectDefaultNamespaceProvider"/>.
    ///
    /// Nothing here touches the disk: the paths are parsed and never opened.
    /// </summary>
    public class TargetNamespaceCalculatorTests
    {
        private const string SolutionFolder = @"c:\solution";
        private const string ProjectFilePath = @"c:\solution\MyApp\MyApp.csproj";

        #region the folder chain

        [Fact]
        public void A_file_in_the_project_folder_has_an_empty_folder_chain()
        {
            var chain = TargetNamespaceCalculator.TryGetFolderChain(
                ProjectFilePath,
                @"c:\solution\MyApp\Class1.cs",
                SettingsWith()
                );

            Assert.NotNull(chain);
            Assert.Empty(chain!);
        }

        [Fact]
        public void Every_folder_between_the_project_and_the_file_takes_a_part_in_the_chain()
        {
            var chain = TargetNamespaceCalculator.TryGetFolderChain(
                ProjectFilePath,
                @"c:\solution\MyApp\Sub\Deep\Class1.cs",
                SettingsWith()
                );

            Assert.Equal(new[] { "Sub", "Deep" }, chain!);
        }

        /// <summary>
        /// A folder excluded by the user is left out of the chain, but its subfolders stay:
        /// the excluded folder is not a border, it is a name which is not written down.
        /// </summary>
        [Fact]
        public void A_skipped_folder_is_left_out_while_its_subfolders_stay()
        {
            var chain = TargetNamespaceCalculator.TryGetFolderChain(
                ProjectFilePath,
                @"c:\solution\MyApp\Impl\Details\Class1.cs",
                SettingsWith(@"MyApp\Impl")
                );

            Assert.Equal(new[] { "Details" }, chain!);
        }

        [Fact]
        public void All_the_folders_of_the_chain_may_be_skipped()
        {
            var chain = TargetNamespaceCalculator.TryGetFolderChain(
                ProjectFilePath,
                @"c:\solution\MyApp\Impl\Details\Class1.cs",
                SettingsWith(@"MyApp\Impl", @"MyApp\Impl\Details")
                );

            Assert.NotNull(chain);
            Assert.Empty(chain!);
        }

        /// <summary>
        /// A file which lies outside of the folder of its project (a linked file) has no
        /// target namespace at all: the rule has nothing to build it of.
        /// </summary>
        [Fact]
        public void A_file_outside_of_the_project_folder_has_no_folder_chain()
        {
            var chain = TargetNamespaceCalculator.TryGetFolderChain(
                ProjectFilePath,
                @"c:\solution\Other\Class1.cs",
                SettingsWith()
                );

            Assert.Null(chain);
        }

        /// <summary>
        /// A file of a sibling folder whose name starts with the name of the project folder
        /// (`MyApp` and `MyApp.Tests`) is outside of the project folder as well, and a linked
        /// file of such a folder is not an exotic case at all.
        ///
        /// The comparison of the folders stops at the folder border, otherwise
        /// `c:\solution\MyApp.Tests\Sub` starts with `c:\solution\MyApp` and the whole sibling
        /// folder becomes a part of the namespace: `MyApp.MyApp.Tests.Sub`.
        /// </summary>
        [Theory]
        [InlineData(@"c:\solution\MyApp.Tests\Sub\Class1.cs")]
        [InlineData(@"c:\solution\MyApp.Tests\Class1.cs")]
        [InlineData(@"c:\solution\MyAppX\Class1.cs")]
        public void A_file_of_a_sibling_folder_with_the_same_prefix_has_no_folder_chain(string documentFilePath)
        {
            var chain = TargetNamespaceCalculator.TryGetFolderChain(
                ProjectFilePath,
                documentFilePath,
                SettingsWith()
                );

            Assert.Null(chain);
        }

        /// <summary>
        /// Windows paths are case insensitive. Roslyn and the IDE may hand the same
        /// folder over in a different case (<c>c:\solution\MyApp</c> vs
        /// <c>C:\solution\MyApp\Sub</c>), and a case-sensitive comparison would treat
        /// a file inside the project folder as a linked one outside of it.
        /// </summary>
        [Fact]
        public void The_case_of_the_path_does_not_matter()
        {
            var chain = TargetNamespaceCalculator.TryGetFolderChain(
                @"c:\solution\MyApp\MyApp.csproj",
                @"C:\solution\MyApp\Sub\Class1.cs",
                SettingsWith()
                );

            Assert.Equal(new[] { "Sub" }, chain!);
        }

        #endregion

        #region composing the name

        [Fact]
        public void A_file_in_the_project_folder_gets_the_default_namespace_of_the_project()
        {
            Assert.Equal(
                "MyApp",
                TargetNamespaceCalculator.Compose("MyApp", new string[0], NoRegex())
                );
        }

        [Fact]
        public void The_folder_chain_is_appended_to_the_default_namespace()
        {
            Assert.Equal(
                "MyApp.Sub.Deep",
                TargetNamespaceCalculator.Compose("MyApp", new[] { "Sub", "Deep" }, NoRegex())
                );
        }

        /// <summary>
        /// The default namespace of a project is not a single identifier
        /// (`&lt;RootNamespace&gt;My.App&lt;/RootNamespace&gt;`).
        /// </summary>
        [Fact]
        public void The_default_namespace_may_contain_dots()
        {
            Assert.Equal(
                "My.App.Sub",
                TargetNamespaceCalculator.Compose("My.App", new[] { "Sub" }, NoRegex())
                );
        }

        /// <summary>
        /// The regex the user types on the second step of the wizard is applied to the whole
        /// name and not to its parts, so it is able to rewrite the border between them.
        /// </summary>
        [Fact]
        public void The_regex_is_applied_to_the_whole_name()
        {
            Assert.Equal(
                "NewRoot.Sub",
                TargetNamespaceCalculator.Compose(
                    "MyApp",
                    new[] { "Sub" },
                    new NamespaceReplaceRegex("^MyApp", "NewRoot")
                    )
                );
        }

        [Fact]
        public void An_incomplete_regex_of_a_typing_user_changes_nothing()
        {
            Assert.Equal(
                "MyApp.Sub",
                TargetNamespaceCalculator.Compose(
                    "MyApp",
                    new[] { "Sub" },
                    new NamespaceReplaceRegex("[unclosed", "NewRoot")
                    )
                );
        }

        #endregion

        #region the fallback for the default namespace

        /// <summary>
        /// The default namespace of a project Visual Studio reports nothing about is the name
        /// of that project without its last part. This is what supports the shared projects:
        /// the code of `MyApp.Shared` belongs to `MyApp`.
        /// </summary>
        [Theory]
        [InlineData("MyApp.Shared", "MyApp")]
        [InlineData("My.App.Shared", "My.App")]
        public void The_last_part_of_the_project_name_is_cut_off(string projectName, string expected)
        {
            Assert.Equal(expected, TargetNamespaceCalculator.DefaultNamespaceFallback(projectName));
        }

        /// <summary>
        /// There is nothing to cut off: a name without a dot, and a name which starts with one.
        /// </summary>
        [Theory]
        [InlineData("MyApp")]
        [InlineData(".Shared")]
        public void A_project_name_without_a_part_to_cut_off_is_taken_as_it_is(string projectName)
        {
            Assert.Equal(projectName, TargetNamespaceCalculator.DefaultNamespaceFallback(projectName));
        }

        #endregion

        private static NamespaceReplaceRegex NoRegex()
        {
            return new NamespaceReplaceRegex(string.Empty, string.Empty);
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
