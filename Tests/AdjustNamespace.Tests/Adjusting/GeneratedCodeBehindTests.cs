using AdjustNamespace.Adjusting;
using AdjustNamespace.Tests.Infrastructure;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The code behind of a xaml file is a partial class whose second part is generated
    /// into the intermediate output folder (<c>obj\...\App.g.i.cs</c>) out of the
    /// <c>x:Class</c> attribute of that xaml.
    ///
    /// Such a generated part is not a declaration of its own: it follows the xaml the
    /// adjusting has just changed, so it neither keeps the old namespace alive nor makes
    /// the moved type a partial one which is split over several files.
    /// </summary>
    public class GeneratedCodeBehindTests
    {
        /// <summary>
        /// The MAUI Windows head: <c>Platforms\Windows\App.xaml.cs</c> declares
        /// <c>TestMauiApp.WinUI.App</c> and is moved into <c>TestMauiApp.Platforms.Windows</c>.
        /// Nothing but the generated part of the very same class stays in the old namespace,
        /// so <c>using TestMauiApp.WinUI;</c> must not be added: the next build regenerates
        /// that part into the target namespace and the clause stops compiling (CS0234).
        /// </summary>
        [Fact]
        public async Task The_old_namespace_of_a_xaml_class_is_not_imported()
        {
            const string GeneratedFilePath = @"obj\Debug\net8.0-windows\Platforms\Windows\App.g.i.cs";

            using var solution = new TestSolution()
                .AddProject("TestMauiApp")
                .AddDocument("TestMauiApp", @"Platforms\Windows\App.xaml.cs",
@"namespace TestMauiApp.WinUI
{
    public partial class App
    {
        public App()
        {
            this.InitializeComponent();
        }
    }
}
")
                .AddDocument("TestMauiApp", GeneratedFilePath,
@"namespace TestMauiApp.WinUI
{
    public partial class App
    {
        public void InitializeComponent()
        {
        }
    }
}
")
                ;

            var xamlFilePath = solution.AddXamlFile("TestMauiApp", @"Platforms\Windows\App.xaml",
@"<maui:MauiWinUIApplication
    x:Class=""TestMauiApp.WinUI.App""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:maui=""using:Microsoft.Maui""
    >
</maui:MauiWinUIApplication>");

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustRunner.AdjustAndCleanupAsync(
                solution,
                "TestMauiApp",
                @"Platforms\Windows\App.xaml.cs",
                "TestMauiApp.Platforms.Windows",
                xamlFilePath
                );

            Assert.Contains(
                @"x:Class=""TestMauiApp.Platforms.Windows.App""",
                solution.XamlTextOf("TestMauiApp", @"Platforms\Windows\App.xaml")
                );

            var text = solution.TextOf("TestMauiApp", @"Platforms\Windows\App.xaml.cs");

            Assert.Contains("namespace TestMauiApp.Platforms.Windows", text);
            Assert.DoesNotContain("using TestMauiApp.WinUI;", text);

            //the build after the adjusting regenerates the code behind out of the new x:Class
            solution.ReplaceDocument("TestMauiApp", GeneratedFilePath,
@"namespace TestMauiApp.Platforms.Windows
{
    public partial class App
    {
        public void InitializeComponent()
        {
        }
    }
}
");

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The generated part is not a reason to keep the old namespace: a using clause of it
        /// has to disappear from the other files of the solution as well.
        /// </summary>
        [Fact]
        public async Task The_old_namespace_of_a_xaml_class_is_removed_from_the_other_files()
        {
            const string GeneratedFilePath = @"obj\Debug\net8.0-windows\App.g.i.cs";

            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"App.xaml.cs",
@"namespace A.B
{
    public partial class App
    {
    }
}
")
                .AddDocument("MyApp", GeneratedFilePath,
@"namespace A.B
{
    public partial class App
    {
    }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public App Create() => new App();
    }
}
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustRunner.AdjustAndCleanupAsync(
                solution,
                "MyApp",
                @"App.xaml.cs",
                "X.Y"
                );

            var text = solution.TextOf("MyApp", "Consumer.cs");

            Assert.Contains("using X.Y;", text);
            Assert.DoesNotContain("using A.B;", text);
        }

        /// <summary>
        /// A partial class which is really split over several files of the user is not
        /// affected: the other file stays in the old namespace and keeps it alive.
        /// </summary>
        [Fact]
        public async Task A_partial_class_of_several_ordinary_files_still_keeps_its_namespace()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.Part1.cs",
@"namespace A.B
{
    public partial class Class1
    {
    }
}
")
                .AddDocument("MyApp", "Class1.Part2.cs",
@"namespace A.B
{
    public partial class Class1
    {
    }
}
")
                ;

            await AdjustRunner.AdjustAndCleanupAsync(
                solution,
                "MyApp",
                "Class1.Part1.cs",
                "X.Y"
                );

            //the second part stays in A.B, so the first one has to import it
            Assert.Contains("using A.B;", solution.TextOf("MyApp", "Class1.Part1.cs"));
        }
    }
}
