using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// MAUI xaml: a different default xmlns and the 2009 xaml language uri. Covered on
    /// this machine by building <c>Tests/Standard/TestMauiApp</c> (Windows TFM).
    /// </summary>
    public class MauiXamlTests
    {
        [Fact]
        public async Task A_maui_page_x_Class_and_its_code_behind_are_adjusted_together()
        {
            using var solution = new TestSolution()
                .AddProject("TestMauiApp")
                .AddDocument("TestMauiApp", @"Views\MainPage.xaml.cs",
@"namespace Old.Views
{
    public partial class MainPage
    {
    }
}
")
                ;

            var xamlFilePath = solution.AddXamlFile("TestMauiApp", @"Views\MainPage.xaml",
@"<ContentPage x:Class=""Old.Views.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
</ContentPage>");

            await AdjustAsync(
                solution,
                "TestMauiApp",
                @"Views\MainPage.xaml.cs",
                "TestMauiApp.Views",
                xamlFilePath
                );

            Assert.Contains(
                @"x:Class=""TestMauiApp.Views.MainPage""",
                solution.XamlTextOf("TestMauiApp", @"Views\MainPage.xaml")
                );
            Assert.Contains(
                "namespace TestMauiApp.Views",
                solution.TextOf("TestMauiApp", @"Views\MainPage.xaml.cs")
                );
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        [Fact]
        public async Task A_maui_clr_namespace_reference_is_fixed_when_the_type_moves()
        {
            using var solution = new TestSolution()
                .AddProject("TestMauiApp")
                .AddDocument("TestMauiApp", @"Controls\MyButton.cs",
@"namespace Old.Controls
{
    public class MyButton { }
}
")
                ;

            var xamlFilePath = solution.AddXamlFile("TestMauiApp", @"Views\MainPage.xaml",
@"<ContentPage x:Class=""TestMauiApp.Views.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
    xmlns:c=""clr-namespace:Old.Controls"">
    <c:MyButton />
</ContentPage>");

            await AdjustAsync(
                solution,
                "TestMauiApp",
                @"Controls\MyButton.cs",
                "TestMauiApp.Controls",
                xamlFilePath
                );

            var xaml = solution.XamlTextOf("TestMauiApp", @"Views\MainPage.xaml");
            Assert.Contains("clr-namespace:TestMauiApp.Controls", xaml);
            Assert.DoesNotContain("clr-namespace:Old.Controls", xaml);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
