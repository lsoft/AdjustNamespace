using AdjustNamespace.Adjusting;
using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.Xaml.BodyProvider;
using System.Linq;
using System.Text;
using Xunit;

namespace AdjustNamespace.Tests.Xaml
{
    /// <summary>
    /// Tests of the way a xaml file is written back to the disk
    /// (<see cref="AdjustNamespace.Xaml.BodyProvider.ClosedXamlBodyProvider"/>).
    ///
    /// The extension edits the file of the user, so everything which is not the subject
    /// of the adjusting — the encoding, the line endings, the formatting — has to survive:
    /// otherwise the diff of the commit consists of the whole file instead of a single line.
    /// </summary>
    public class XamlFileWritingTests
    {
        private const string Body =
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>";

        /// <summary>
        /// Visual Studio writes the xaml files as UTF-8 with a byte order mark.
        /// </summary>
        [Fact]
        public async Task The_byte_order_mark_of_the_file_survives()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml", Body, new UTF8Encoding(true));

            await new XamlAdjuster(new ClosedXamlBodyProviderFactory(), false, AdjustPlanItem.Xaml(xamlFilePath, "X.Y")).AdjustAsync();

            var bytes = solution.XamlBytesOf("MyApp", "MainWindow.xaml");

            Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3).ToArray());
            Assert.Contains(@"x:Class=""X.Y.MainWindow""", solution.XamlTextOf("MyApp", "MainWindow.xaml"));
        }

        /// <summary>
        /// A file without a byte order mark must not get one.
        /// </summary>
        [Fact]
        public async Task A_file_without_a_byte_order_mark_does_not_get_one()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml", Body, new UTF8Encoding(false));

            await new XamlAdjuster(new ClosedXamlBodyProviderFactory(), false, AdjustPlanItem.Xaml(xamlFilePath, "X.Y")).AdjustAsync();

            var bytes = solution.XamlBytesOf("MyApp", "MainWindow.xaml");

            Assert.NotEqual(Encoding.UTF8.GetPreamble(), bytes.Take(3).ToArray());
        }

        /// <summary>
        /// The non ASCII content (a caption in the national language) has to stay readable.
        /// </summary>
        [Fact]
        public async Task The_non_ascii_content_survives()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var body =
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <TextBlock Text=""Привет, мир"" />
</Window>";

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml", body, new UTF8Encoding(true));

            await new XamlAdjuster(new ClosedXamlBodyProviderFactory(), false, AdjustPlanItem.Xaml(xamlFilePath, "X.Y")).AdjustAsync();

            Assert.Contains(@"Text=""Привет, мир""", solution.XamlTextOf("MyApp", "MainWindow.xaml"));
        }

        /// <summary>
        /// Only the <c>x:Class</c> attribute is a subject to change: the line endings
        /// of the rest of the file have to stay untouched.
        /// </summary>
        [Fact]
        public async Task The_line_endings_are_not_changed()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                ;

            var body = Body.Replace("\r\n", "\n");

            var xamlFilePath = solution.AddXamlFile("MyApp", "MainWindow.xaml", body, new UTF8Encoding(false));

            await new XamlAdjuster(new ClosedXamlBodyProviderFactory(), false, AdjustPlanItem.Xaml(xamlFilePath, "X.Y")).AdjustAsync();

            var text = solution.XamlTextOf("MyApp", "MainWindow.xaml");

            Assert.DoesNotContain("\r\n", text);
            Assert.Contains(@"x:Class=""X.Y.MainWindow""", text);
        }
    }
}
