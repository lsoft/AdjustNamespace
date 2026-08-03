using AdjustNamespace.Adjusting;
using AdjustNamespace.Tests.Infrastructure;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of <see cref="XamlAdjuster"/>: it moves the root class of a xaml document
    /// (its <c>x:Class</c> attribute) into the target namespace. The code behind file
    /// is a usual C# file and is processed by <see cref="CsAdjuster"/> separately.
    ///
    /// The adjuster is created with <c>openFilesToEnableUndo: false</c>, so it works
    /// with the file itself and needs no Visual Studio editor.
    /// </summary>
    public class XamlAdjusterTests
    {
        [Fact]
        public async Task The_root_class_of_the_document_is_moved()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml",
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            var adjusted = await new XamlAdjuster(solution.Services, false, xamlFilePath, "X.Y").AdjustAsync();

            Assert.True(adjusted);
            Assert.Contains(@"x:Class=""X.Y.MainWindow""", solution.XamlTextOf("MyApp", "MainWindow.xaml"));
        }

        [Fact]
        public async Task A_document_which_is_in_the_target_namespace_already_is_not_changed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            const string Body =
@"<Window x:Class=""X.Y.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>";

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml", Body);

            var adjuster = new XamlAdjuster(solution.Services, false, xamlFilePath, "X.Y");

            Assert.False(await adjuster.IsChangesExistsAsync());
            Assert.False(await adjuster.AdjustAsync());
            Assert.Equal(Body, solution.XamlTextOf("MyApp", "MainWindow.xaml"));
        }

        /// <summary>
        /// A ResourceDictionary has no <c>x:Class</c> at all, so there is nothing to move.
        /// </summary>
        [Fact]
        public async Task A_document_without_the_root_class_is_not_changed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            const string Body =
@"<ResourceDictionary
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
</ResourceDictionary>";

            var xamlFilePath = solution.AddXamlFile("MyApp", "Styles.xaml", Body);

            var adjuster = new XamlAdjuster(solution.Services, false, xamlFilePath, "X.Y");

            Assert.False(await adjuster.IsChangesExistsAsync());
            Assert.False(await adjuster.AdjustAsync());
            Assert.Equal(Body, solution.XamlTextOf("MyApp", "Styles.xaml"));
        }

        /// <summary>
        /// The second step of the wizard asks every file whether it is going to change,
        /// long before the user has confirmed anything: such a question must not modify
        /// the file.
        /// </summary>
        [Fact]
        public async Task The_check_for_the_changes_does_not_modify_the_file()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            const string Body =
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>";

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml", Body);

            var adjuster = new XamlAdjuster(solution.Services, false, xamlFilePath, "X.Y");

            Assert.True(await adjuster.IsChangesExistsAsync());
            Assert.Equal(Body, solution.XamlTextOf("MyApp", "MainWindow.xaml"));
        }

        /// <summary>
        /// Only the root class is a subject of this adjuster: the tags of the document
        /// reference the other classes, which are moved by <see cref="CsAdjuster"/>
        /// when their own files are adjusted.
        /// </summary>
        [Fact]
        public async Task The_tags_of_the_document_are_not_touched()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml",
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton />
</Window>");

            await new XamlAdjuster(solution.Services, false, xamlFilePath, "X.Y").AdjustAsync();

            var xaml = solution.XamlTextOf("MyApp", "MainWindow.xaml");

            Assert.Contains(@"x:Class=""X.Y.MainWindow""", xaml);
            Assert.Contains("<local:MyButton />", xaml);
            Assert.Contains("clr-namespace:A.B", xaml);
        }
    }
}
