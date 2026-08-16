using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Tests.Infrastructure;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Avalonia: <c>.axaml</c> files and the preferred <c>using:</c> xmlns form.
    /// Covered on this machine by building a minimal Avalonia desktop app.
    /// </summary>
    public class AvaloniaXamlTests
    {
        [Fact]
        public async Task An_axaml_file_is_planned_as_xaml()
        {
            using var solution = new TestSolution()
                .AddProject("SmokeAvalonia")
                ;

            var axamlPath = solution.AddXamlFile("SmokeAvalonia", "MainWindow.axaml",
@"<Window x:Class=""Old.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            var plan = await new AdjustPlanner(solution.Context, new NamespaceReplaceRegex(string.Empty, string.Empty))
                .TryPlanAsync(axamlPath);

            Assert.NotNull(plan);
            Assert.True(plan!.Value.IsXaml);
            Assert.Equal("SmokeAvalonia", plan.Value.TargetNamespace);
        }

        [Fact]
        public async Task An_axaml_root_class_and_its_code_behind_are_adjusted()
        {
            using var solution = new TestSolution()
                .AddProject("SmokeAvalonia")
                .AddDocument("SmokeAvalonia", "MainWindow.axaml.cs",
@"namespace Old
{
    public partial class MainWindow
    {
    }
}
")
                ;

            var axamlPath = solution.AddXamlFile("SmokeAvalonia", "MainWindow.axaml",
@"<Window x:Class=""Old.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            await AdjustAsync(
                solution,
                "SmokeAvalonia",
                "MainWindow.axaml.cs",
                "SmokeAvalonia",
                axamlPath
                );

            Assert.Contains(
                @"x:Class=""SmokeAvalonia.MainWindow""",
                solution.XamlTextOf("SmokeAvalonia", "MainWindow.axaml")
                );
            Assert.Contains(
                "namespace SmokeAvalonia",
                solution.TextOf("SmokeAvalonia", "MainWindow.axaml.cs")
                );
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        [Fact]
        public async Task A_using_xmlns_reference_in_axaml_is_fixed_when_the_type_moves()
        {
            using var solution = new TestSolution()
                .AddProject("SmokeAvalonia")
                .AddDocument("SmokeAvalonia", @"Controls\MyButton.cs",
@"namespace Old.Controls
{
    public class MyButton { }
}
")
                ;

            var axamlPath = solution.AddXamlFile("SmokeAvalonia", "MainWindow.axaml",
@"<Window x:Class=""SmokeAvalonia.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:c=""using:Old.Controls"">
    <c:MyButton />
</Window>");

            await AdjustAsync(
                solution,
                "SmokeAvalonia",
                @"Controls\MyButton.cs",
                "SmokeAvalonia.Controls",
                axamlPath
                );

            var axaml = solution.XamlTextOf("SmokeAvalonia", "MainWindow.axaml");
            Assert.Contains("using:SmokeAvalonia.Controls", axaml);
            Assert.DoesNotContain("using:Old.Controls", axaml);
            Assert.DoesNotContain("clr-namespace:", axaml);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }
    }
}
