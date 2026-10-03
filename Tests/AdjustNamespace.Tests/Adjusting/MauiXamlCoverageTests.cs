using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.Xaml.BodyProvider;
using System.Text.RegularExpressions;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The places of a .NET MAUI xaml file which can mention a CLR namespace or a CLR type
    /// and therefore break when the type moves: compiled bindings (<c>x:DataType</c>),
    /// the Shell templates, styles and triggers, the generic types, the factory methods,
    /// the markup extensions, the resource dictionaries, the platform heads, the implicit and
    /// the global xmlns of .NET MAUI 10, the code generated out of a xaml file.
    ///
    /// <see cref="MauiXamlTests"/> covers the basics (<c>x:Class</c> and a <c>clr-namespace</c>
    /// tag), the other xaml tests cover the xaml syntax in general. The tests here follow the
    /// shapes of <c>Tests/Standard/TestMauiApp</c> and run the whole pipeline: the C# type
    /// moves, the xaml file is rewritten, the solution still compiles.
    /// </summary>
    public class MauiXamlCoverageTests
    {
        private const string Project = "TestMauiApp";

        private const string XamlPath = @"Views\MainPage.xaml";

        #region The type references of a page

        /// <summary>
        /// The template of the Shell: <c>ContentTemplate="{DataTemplate local:MainPage}"</c>
        /// is neither a tag nor a <c>{x:Type}</c>, the reference is a bare pair inside of
        /// a <c>DataTemplate</c> markup extension. The <c>local</c> alias of the template
        /// (<c>clr-namespace:TestMauiApp</c>) is not needed anymore afterwards.
        /// </summary>
        [Fact]
        public async Task A_shell_content_template_reference_to_a_moved_page_is_fixed()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Views\MainPage.xaml.cs",
@"namespace TestMauiApp
{
    public partial class MainPage
    {
    }
}
")
                .AddDocument(Project, "AppShell.xaml.cs",
@"namespace TestMauiApp
{
    public partial class AppShell
    {
    }
}
")
                ;

            var pagePath = solution.AddXamlFile(Project, XamlPath,
@"<ContentPage x:Class=""TestMauiApp.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
</ContentPage>");

            var shellPath = solution.AddXamlFile(Project, "AppShell.xaml",
@"<Shell
    x:Class=""TestMauiApp.AppShell""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
    xmlns:local=""clr-namespace:TestMauiApp""
    Shell.FlyoutBehavior=""Disabled"">

    <ShellContent
        Title=""Home""
        ContentTemplate=""{DataTemplate local:MainPage}""
        Route=""MainPage"" />

</Shell>");

            await AdjustAndCleanupAsync(solution, Project, @"Views\MainPage.xaml.cs", "TestMauiApp.Views", pagePath, shellPath);

            var shell = solution.XamlTextOf(Project, "AppShell.xaml");
            var views = AliasOf(shell, "TestMauiApp.Views");

            Assert.Contains("ContentTemplate=\"{DataTemplate " + views + ":MainPage}\"", shell);
            Assert.DoesNotContain("local:", shell);
            Assert.Contains(@"x:Class=""TestMauiApp.AppShell""", shell);
            Assert.Contains(@"x:Class=""TestMauiApp.Views.MainPage""", solution.XamlTextOf(Project, XamlPath));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A compiled binding: <c>x:DataType</c> on the page itself and on a
        /// <c>DataTemplate</c> is a bare attribute value, and the view model of a page
        /// is also created by a property element of <c>BindingContext</c>.
        /// </summary>
        [Fact]
        public async Task Compiled_binding_types_and_the_binding_context_are_fixed()
        {
            using var solution = Solution(
                @"ViewModels\ViewModels.cs",
                "Old.ViewModels",
                "public class MainViewModel { } public class ItemViewModel { }"
                );

            var xaml = await MoveAsync(
                solution,
                @"ViewModels\ViewModels.cs",
                "TestMauiApp.ViewModels",
                Page(
@"xmlns:vm=""clr-namespace:Old.ViewModels""
    x:DataType=""vm:MainViewModel""",
@"    <ContentPage.BindingContext>
        <vm:MainViewModel />
    </ContentPage.BindingContext>
    <CollectionView>
        <CollectionView.ItemTemplate>
            <DataTemplate x:DataType=""vm:ItemViewModel"">
                <Label Text=""{Binding Name}"" />
            </DataTemplate>
        </CollectionView.ItemTemplate>
    </CollectionView>")
                );

            var vm = AliasOf(xaml, "TestMauiApp.ViewModels");

            Assert.Contains(@"x:DataType=""" + vm + @":MainViewModel""", xaml);
            Assert.Contains(@"x:DataType=""" + vm + @":ItemViewModel""", xaml);
            Assert.Contains("<" + vm + ":MainViewModel />", xaml);
            Assert.DoesNotContain("vm:", xaml);
        }

        /// <summary>
        /// <c>RelativeSource AncestorType={x:Type local:MainPage}</c>: the <c>{x:Type}</c>
        /// is nested into a markup extension which is nested into a binding.
        /// </summary>
        [Fact]
        public async Task A_relative_source_ancestor_type_is_fixed()
        {
            using var solution = Solution(@"Pages\DetailPage.cs", "Old.Pages", "public class DetailPage { }");

            var xaml = await MoveAsync(
                solution,
                @"Pages\DetailPage.cs",
                "TestMauiApp.Pages",
                Page(
@"xmlns:pages=""clr-namespace:Old.Pages""",
@"    <Label Text=""{Binding Source={RelativeSource AncestorType={x:Type pages:DetailPage}}, Path=Title}"" />")
                );

            var pages = AliasOf(xaml, "TestMauiApp.Pages");

            Assert.Contains("AncestorType={x:Type " + pages + ":DetailPage}", xaml);
            Assert.DoesNotContain("pages:", xaml);
        }

        /// <summary>
        /// <c>{x:Static}</c> as the source of a binding.
        /// </summary>
        [Fact]
        public async Task An_x_Static_source_of_a_binding_is_fixed()
        {
            using var solution = Solution(
                @"Controls\Settings.cs",
                "Old.Controls",
                "public class Settings { public static Settings Instance { get; } = new Settings(); public string Name => \"\"; }"
                );

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Settings.cs",
@"    <Label Text=""{Binding Source={x:Static c:Settings.Instance}, Path=Name}"" />"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Contains("Source={x:Static " + c + ":Settings.Instance}", xaml);
            Assert.DoesNotMatch(@"\bc:", xaml);
        }

        /// <summary>
        /// An enum is a moved type like any other: <c>{x:Static}</c> of its member and
        /// <c>{x:Type}</c> of it.
        /// </summary>
        [Fact]
        public async Task An_enum_referenced_by_x_Static_and_x_Type_is_fixed()
        {
            using var solution = Solution(@"Controls\Mode.cs", "Old.Controls", "public enum Mode { Fast, Slow }");

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Mode.cs",
@"    <Label Text=""{x:Static c:Mode.Fast}"" />
    <Label Tag=""{x:Type c:Mode}"" />"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Contains("{x:Static " + c + ":Mode.Fast}", xaml);
            Assert.Contains("{x:Type " + c + ":Mode}", xaml);
        }

        /// <summary>
        /// A nested class is written <c>Outer+Inner</c> in xaml: the owner of it is the
        /// moved type.
        /// </summary>
        [Fact]
        public async Task A_nested_type_written_with_a_plus_is_fixed()
        {
            using var solution = Solution(
                @"Controls\Outer.cs",
                "Old.Controls",
                "public class Outer { public class Inner { public const string Value = \"v\"; } }"
                );

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Outer.cs",
@"    <Label Text=""{x:Static c:Outer+Inner.Value}"" />"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Contains("{x:Static " + c + ":Outer+Inner.Value}", xaml);
        }

        /// <summary>
        /// Was red: <c>CsAdjuster</c> hands every type of the file to <c>XamlDocument.MoveObject</c>
        /// by its simple name, nested types included, and the xaml part cannot tell
        /// <c>Host.Item</c> from a top level <c>Item</c> of the same old namespace. The page
        /// uses the <c>Item</c> which stays where it is, but the tag follows the nested one
        /// and points to a type which does not exist (the xaml compiler fails on the next build).
        /// A nested type is no member of the namespace, so it must not be moved by name.
        /// </summary>
        [Fact]
        public async Task A_nested_type_does_not_move_a_top_level_type_of_the_same_name()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Controls\Host.cs",
@"namespace Old.Controls
{
    public class Host
    {
        public class Item { }
    }
}
")
                .AddDocument(Project, @"Controls\Item.cs",
@"namespace Old.Controls
{
    public class Item { }
}
")
                ;

            var xaml = await MoveAsync(
                solution,
                @"Controls\Host.cs",
                "TestMauiApp.Hosts",
                Page(
@"xmlns:c=""clr-namespace:Old.Controls""",
@"    <c:Item />")
                );

            Assert.Contains("<c:Item />", xaml);
            Assert.Contains(@"xmlns:c=""clr-namespace:Old.Controls""", xaml);
        }

        /// <summary>
        /// One file with several types, all of them used by the page: the second type reuses
        /// the xmlns clause the first one created, a single clause is written.
        /// </summary>
        [Fact]
        public async Task Several_types_of_one_file_share_the_new_xmlns()
        {
            using var solution = Solution(
                @"Controls\Controls.cs",
                "Old.Controls",
                "public class MyButton { } public class MyLabel { } public static class Helper { }"
                );

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Controls.cs",
@"    <c:MyButton c:Helper.IsEnabled=""True"" />
    <c:MyLabel />"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Equal(1, CountOf(xaml, "clr-namespace:TestMauiApp.Controls"));
            Assert.DoesNotContain("clr-namespace:Old.Controls", xaml);
            Assert.Contains("<" + c + ":MyButton " + c + ":Helper.IsEnabled=\"True\" />", xaml);
            Assert.Contains("<" + c + ":MyLabel />", xaml);
        }

        #endregion

        #region Styles, triggers, resources

        /// <summary>
        /// A style of the moved control: <c>TargetType</c>, a setter of an attached property
        /// (<c>Property="local:Helper.IsEnabled"</c>), an attached property of a tag and a
        /// property element of an attached property. <c>BasedOn</c> is the key of a resource
        /// and has nothing to do with a namespace.
        /// </summary>
        [Fact]
        public async Task A_style_with_an_attached_property_setter_is_fixed()
        {
            using var solution = Solution(
                @"Controls\Controls.cs",
                "Old.Controls",
                "public class MyButton { } public static class Helper { }"
                );

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Controls.cs",
@"    <ContentPage.Resources>
        <Style x:Key=""Derived"" TargetType=""c:MyButton"" BasedOn=""{StaticResource BaseButton}"">
            <Setter Property=""c:Helper.IsEnabled"" Value=""True"" />
        </Style>
    </ContentPage.Resources>
    <Grid c:Helper.IsEnabled=""False"">
        <c:Helper.Items>
            <Label />
        </c:Helper.Items>
    </Grid>"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Contains(@"TargetType=""" + c + @":MyButton""", xaml);
            Assert.Contains(@"Property=""" + c + @":Helper.IsEnabled""", xaml);
            Assert.Contains(@"<Grid " + c + @":Helper.IsEnabled=""False"">", xaml);
            Assert.Contains("<" + c + ":Helper.Items>", xaml);
            Assert.Contains("</" + c + ":Helper.Items>", xaml);
            Assert.Contains(@"BasedOn=""{StaticResource BaseButton}""", xaml);
            Assert.DoesNotMatch(@"\bc:", xaml);
        }

        /// <summary>
        /// A <c>DataTrigger</c> names the type it applies to, an <c>EventTrigger</c> holds a
        /// trigger action class, both inside the property element of the moved control.
        /// </summary>
        [Fact]
        public async Task A_data_trigger_and_a_trigger_action_are_fixed()
        {
            using var solution = Solution(
                @"Controls\Controls.cs",
                "Old.Controls",
                "public class MyButton { } public class ShakeAction { }"
                );

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Controls.cs",
@"    <c:MyButton>
        <c:MyButton.Triggers>
            <DataTrigger TargetType=""c:MyButton"" Binding=""{Binding IsOn}"" Value=""True"">
                <Setter Property=""Text"" Value=""On"" />
            </DataTrigger>
            <EventTrigger Event=""Clicked"">
                <c:ShakeAction />
            </EventTrigger>
        </c:MyButton.Triggers>
    </c:MyButton>"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Contains(@"<DataTrigger TargetType=""" + c + @":MyButton""", xaml);
            Assert.Contains("<" + c + ":ShakeAction />", xaml);
            Assert.Contains("<" + c + ":MyButton.Triggers>", xaml);
            Assert.DoesNotMatch(@"\bc:", xaml);
        }

        /// <summary>
        /// A converter, a <c>DataTemplateSelector</c> with its template properties and a
        /// behavior are declared as tags in the resources and in property elements.
        /// </summary>
        [Fact]
        public async Task A_converter_a_template_selector_and_a_behavior_are_fixed()
        {
            using var solution = Solution(
                @"Controls\Controls.cs",
                "Old.Controls",
                "public class BoolToColorConverter { } public class TemplateChooser { } public class NumericBehavior { }"
                );

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Controls.cs",
@"    <ContentPage.Resources>
        <c:BoolToColorConverter x:Key=""conv"" />
        <c:TemplateChooser x:Key=""chooser"">
            <c:TemplateChooser.Even>
                <DataTemplate><Label Text=""even"" /></DataTemplate>
            </c:TemplateChooser.Even>
        </c:TemplateChooser>
    </ContentPage.Resources>
    <Entry Text=""{Binding Value, Converter={StaticResource conv}}"">
        <Entry.Behaviors>
            <c:NumericBehavior MaxDigits=""3"" />
        </Entry.Behaviors>
    </Entry>
    <CollectionView ItemTemplate=""{StaticResource chooser}"" />"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Contains("<" + c + ":BoolToColorConverter x:Key=\"conv\" />", xaml);
            Assert.Contains("<" + c + ":TemplateChooser x:Key=\"chooser\">", xaml);
            Assert.Contains("<" + c + ":TemplateChooser.Even>", xaml);
            Assert.Contains("</" + c + ":TemplateChooser.Even>", xaml);
            Assert.Contains("</" + c + ":TemplateChooser>", xaml);
            Assert.Contains("<" + c + ":NumericBehavior MaxDigits=\"3\" />", xaml);
            Assert.DoesNotMatch(@"\bc:", xaml);
        }

        #endregion

        #region Generics, factories, markup extensions

        /// <summary>
        /// <c>x:TypeArguments</c> of <c>OnPlatform</c> / <c>OnIdiom</c>, a list of them, a
        /// generic class used by its name without the arity, and the <c>Type</c> of
        /// <c>x:Array</c>.
        /// </summary>
        [Fact]
        public async Task Type_arguments_of_platform_and_generic_elements_are_fixed()
        {
            using var solution = Solution(
                @"Controls\Controls.cs",
                "Old.Controls",
                "public class Item { } public class Pair<TKey, TValue> { }"
                );

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Controls.cs",
@"    <ContentPage.Resources>
        <OnPlatform x:Key=""a"" x:TypeArguments=""c:Item"">
            <On Platform=""WinUI""><c:Item /></On>
        </OnPlatform>
        <OnIdiom x:Key=""b"" x:TypeArguments=""c:Item"" />
        <c:Pair x:Key=""p"" x:TypeArguments=""x:String, c:Item"" />
        <x:Array x:Key=""arr"" Type=""{x:Type c:Item}"" />
    </ContentPage.Resources>"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Contains(@"<OnPlatform x:Key=""a"" x:TypeArguments=""" + c + @":Item"">", xaml);
            Assert.Contains(@"<OnIdiom x:Key=""b"" x:TypeArguments=""" + c + @":Item"" />", xaml);
            Assert.Contains("<" + c + @":Pair x:Key=""p"" x:TypeArguments=""x:String, " + c + @":Item"" />", xaml);
            Assert.Contains("Type=\"{x:Type " + c + ":Item}\"", xaml);
            Assert.Contains("<On Platform=\"WinUI\"><" + c + ":Item /></On>", xaml);
            Assert.DoesNotMatch(@"\bc:", xaml);
        }

        /// <summary>
        /// A generic base page: the root element is a moved generic class and its type
        /// argument is a moved view model (<c>x:TypeArguments</c> of the root element).
        /// </summary>
        [Fact]
        public async Task A_generic_root_page_and_its_type_argument_are_fixed()
        {
            using var solution = Solution(
                @"Core\Core.cs",
                "Old.Core",
                "public class BasePage<TViewModel> { } public class MainViewModel { }"
                );

            var xamlPath = solution.AddXamlFile(Project, XamlPath,
@"<core:BasePage x:Class=""TestMauiApp.Views.MainPage""
    x:TypeArguments=""core:MainViewModel""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
    xmlns:core=""clr-namespace:Old.Core"">
</core:BasePage>");

            await AdjustAsync(solution, Project, @"Core\Core.cs", "TestMauiApp.Core", xamlPath);

            var xaml = solution.XamlTextOf(Project, XamlPath);
            var core = AliasOf(xaml, "TestMauiApp.Core");

            Assert.Contains("<" + core + ":BasePage", xaml);
            Assert.Contains("</" + core + ":BasePage>", xaml);
            Assert.Contains(@"x:TypeArguments=""" + core + @":MainViewModel""", xaml);
            Assert.DoesNotContain("core:", xaml);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// <c>x:FactoryMethod</c> names a method and not a type, <c>x:Arguments</c> holds the
        /// elements of the moved type: only the tags follow the class.
        /// </summary>
        [Fact]
        public async Task A_factory_method_and_the_arguments_of_a_moved_class_are_fixed()
        {
            using var solution = Solution(
                @"Controls\Controls.cs",
                "Old.Controls",
                "public class Product { public static Product Create(Size size) => new Product(); } public class Size { }"
                );

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Controls.cs",
@"    <ContentPage.Resources>
        <c:Product x:Key=""p"" x:FactoryMethod=""Create"">
            <x:Arguments>
                <c:Size />
            </x:Arguments>
        </c:Product>
    </ContentPage.Resources>"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Contains("<" + c + @":Product x:Key=""p"" x:FactoryMethod=""Create"">", xaml);
            Assert.Contains("<" + c + ":Size />", xaml);
            Assert.Contains("</" + c + ":Product>", xaml);
            Assert.DoesNotMatch(@"\bc:", xaml);
        }

        /// <summary>
        /// The class of a markup extension ends with <c>Extension</c>, xaml may drop the suffix
        /// (<c>{c:UpperCase}</c>) or keep it (<c>{c:LocalizeExtension}</c>); both are references.
        /// </summary>
        [Fact]
        public async Task A_markup_extension_with_and_without_the_suffix_is_fixed()
        {
            using var solution = Solution(
                @"Controls\Extensions.cs",
                "Old.Controls",
                "public class UpperCaseExtension { public string Text { get; set; } = \"\"; } public class LocalizeExtension { public string Key { get; set; } = \"\"; }"
                );

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\Extensions.cs",
@"    <Label Text=""{c:UpperCase Text=abc}"" Title=""{c:LocalizeExtension Key=Hello}"" />"
                );

            var c = AliasOf(xaml, "TestMauiApp.Controls");

            Assert.Contains("{" + c + ":UpperCase Text=abc}", xaml);
            Assert.Contains("{" + c + ":LocalizeExtension Key=Hello}", xaml);
            Assert.DoesNotMatch(@"\bc:", xaml);
        }

        /// <summary>
        /// A binding path may name an attached property in parentheses:
        /// <c>Path=(local:Helper.Count)</c>.
        /// </summary>
        [Fact]
        public void An_attached_property_in_a_binding_path_is_fixed()
        {
            var result = MemoryXamlBodyProvider.MoveObject(
                Page(
@"xmlns:c=""clr-namespace:A.B""",
@"    <Label Text=""{Binding Path=(c:Helper.Count)}"" />"),
                "A.B",
                "Helper",
                "X.Y"
                );

            Assert.Contains("clr-namespace:X.Y", result);
            Assert.DoesNotContain("(c:Helper", result);
        }

        #endregion

        #region Resource dictionaries

        /// <summary>
        /// A <c>ResourceDictionary</c> with its own <c>x:Class</c> and a code behind: the root
        /// class follows the code behind, the merged dictionary which refers to the class by
        /// a tag follows it too, and the one which refers to a file by its path
        /// (<c>Source="Resources/Styles/Colors.xaml"</c>) is untouched: the extension moves
        /// namespaces, never files.
        /// </summary>
        [Fact]
        public async Task A_resource_dictionary_with_a_code_behind_is_fixed()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Resources\Theme.xaml.cs",
@"namespace Old.Resources
{
    public partial class Theme
    {
    }
}
")
                ;

            var themePath = solution.AddXamlFile(Project, @"Resources\Theme.xaml",
@"<ResourceDictionary x:Class=""Old.Resources.Theme""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
</ResourceDictionary>");

            var appPath = solution.AddXamlFile(Project, "App.xaml",
@"<Application x:Class=""TestMauiApp.App""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
    xmlns:res=""clr-namespace:Old.Resources"">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <res:Theme />
                <ResourceDictionary Source=""Resources/Styles/Colors.xaml"" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>");

            await AdjustAsync(solution, Project, @"Resources\Theme.xaml.cs", "TestMauiApp.Styles", themePath, appPath);

            var app = solution.XamlTextOf(Project, "App.xaml");
            var styles = AliasOf(app, "TestMauiApp.Styles");

            Assert.Contains(@"x:Class=""TestMauiApp.Styles.Theme""", solution.XamlTextOf(Project, @"Resources\Theme.xaml"));
            Assert.Contains("<" + styles + ":Theme />", app);
            Assert.Contains(@"<ResourceDictionary Source=""Resources/Styles/Colors.xaml"" />", app);
            Assert.DoesNotContain("res:", app);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A path or a text with a colon is no type reference even when its last part is
        /// the name of the moved class.
        /// </summary>
        [Fact]
        public void A_resource_dictionary_source_path_is_not_a_type_reference()
        {
            var result = MemoryXamlBodyProvider.MoveObject(
                Page(
@"xmlns:c=""clr-namespace:A.B""",
@"    <ResourceDictionary Source=""Resources/Styles/Colors.xaml"" />
    <ResourceDictionary Source=""ms-appx:///Colors.xaml"" />
    <Label Text=""Colors: red"" />
    <c:Colors />"),
                "A.B",
                "Colors",
                "X.Y"
                );

            Assert.Contains(@"Source=""Resources/Styles/Colors.xaml""", result);
            Assert.Contains(@"Source=""ms-appx:///Colors.xaml""", result);
            Assert.Contains(@"Text=""Colors: red""", result);
            Assert.DoesNotContain("<c:Colors", result);
        }

        #endregion

        #region The xmlns forms

        /// <summary>
        /// MAUI understands the <c>using:</c> form of a mapping as well as
        /// <c>clr-namespace:</c>; the rewritten mapping keeps the form the user has written.
        /// </summary>
        [Fact]
        public async Task A_using_xmlns_of_a_maui_page_is_rewritten_as_using()
        {
            using var solution = Solution(@"Controls\MyButton.cs", "Old.Controls", "public class MyButton { }");

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\MyButton.cs",
                "    <c:MyButton />",
                "using:Old.Controls"
                );

            Assert.Contains(@"=""using:TestMauiApp.Controls""", xaml);
            Assert.DoesNotContain("clr-namespace:", xaml);
            Assert.DoesNotContain("Old.Controls", xaml);
        }

        /// <summary>
        /// A mapping which names the own assembly explicitly keeps the assembly part.
        /// </summary>
        [Fact]
        public async Task An_xmlns_with_the_own_assembly_keeps_the_assembly_part()
        {
            using var solution = Solution(@"Controls\MyButton.cs", "Old.Controls", "public class MyButton { }");

            var xaml = await MoveIntoControlsAsync(
                solution,
                @"Controls\MyButton.cs",
                "    <c:MyButton />",
                "clr-namespace:Old.Controls;assembly=TestMauiApp"
                );

            Assert.Contains(@"=""clr-namespace:TestMauiApp.Controls;assembly=TestMauiApp""", xaml);
            Assert.DoesNotContain("Old.Controls", xaml);
        }

        /// <summary>
        /// Was red: a mapping is matched by the namespace only. <c>lib:MyButton</c> lives in
        /// the <c>Lib</c> assembly which has a namespace and a class of the very same names;
        /// it is not the moved class, but its mapping is rewritten into
        /// <c>clr-namespace:TestMauiApp.Controls;assembly=Lib</c>, which does not exist.
        /// <c>XamlXmlns.Suffix</c> (the <c>assembly=</c> part) has to be compared with the
        /// assembly of the moved type.
        /// </summary>
        [Fact]
        public async Task An_xmlns_of_another_assembly_with_the_same_namespace_and_name_is_not_moved()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddProject("Lib")
                .AddDocument(Project, @"Controls\MyButton.cs", "namespace Old.Controls { public class MyButton { } }")
                .AddDocument("Lib", "MyButton.cs", "namespace Old.Controls { public class MyButton { } }")
                ;

            var xaml = await MoveAsync(
                solution,
                @"Controls\MyButton.cs",
                "TestMauiApp.Controls",
                Page(
@"xmlns:c=""clr-namespace:Old.Controls""
    xmlns:lib=""clr-namespace:Old.Controls;assembly=Lib""",
@"    <c:MyButton />
    <lib:MyButton />")
                );

            Assert.Contains(@"xmlns:lib=""clr-namespace:Old.Controls;assembly=Lib""", xaml);
            Assert.Contains("<lib:MyButton />", xaml);
        }

        /// <summary>
        /// Was red: <c>MoveObject</c> puts the new xmlns clause behind the last clause of the whole
        /// document. When a nested element declares one (a <c>ResourceDictionary</c> of the
        /// page resources), the new alias is declared in the start tag of that element and
        /// is not visible to the tags of the page outside of it: the page stops parsing.
        /// The new clause has to go to the root element.
        /// </summary>
        [Fact]
        public void A_new_xmlns_is_declared_on_the_root_element_and_not_on_a_nested_one()
        {
            var result = MemoryXamlBodyProvider.MoveObject(
@"<ContentPage x:Class=""A.B.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <ContentPage.Resources>
        <ResourceDictionary xmlns:other=""clr-namespace:Q.W"">
            <other:Converter x:Key=""c"" />
        </ResourceDictionary>
    </ContentPage.Resources>
    <local:MyButton />
</ContentPage>",
                "A.B",
                "MyButton",
                "X.Y"
                );

            var declaration = result.IndexOf("clr-namespace:X.Y", StringComparison.Ordinal);
            var endOfRootTag = result.IndexOf('>');

            Assert.True(declaration >= 0);
            Assert.True(declaration < endOfRootTag, "the new xmlns is not declared by the root element:" + Environment.NewLine + result);
        }

        /// <summary>
        /// Was red: the attributes are recognized with the double quotes only, but xml allows the
        /// single ones. A page written with them keeps its old namespace everywhere.
        /// </summary>
        [Fact]
        public void Single_quoted_attributes_are_processed()
        {
            var result = MemoryXamlBodyProvider.MoveObject(
@"<ContentPage x:Class='A.B.MainPage'
    xmlns='http://schemas.microsoft.com/dotnet/2021/maui'
    xmlns:x='http://schemas.microsoft.com/winfx/2009/xaml'
    xmlns:local='clr-namespace:A.B'>
    <local:MyButton />
</ContentPage>",
                "A.B",
                "MyButton",
                "X.Y"
                );

            Assert.Contains("clr-namespace:X.Y", result);
            Assert.DoesNotContain("<local:MyButton", result);
        }

        /// <summary>
        /// Was red: xml allows a hyphen in a prefix (<c>xmlns:my-ctrl</c>), the regexes know letters,
        /// digits and the underscore only, so the mapping, and every tag with this prefix, are
        /// invisible to the extension.
        /// </summary>
        [Fact]
        public void A_prefix_with_a_hyphen_is_processed()
        {
            var result = MemoryXamlBodyProvider.MoveObject(
@"<ContentPage x:Class=""A.B.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
    xmlns:my-ctrl=""clr-namespace:A.B"">
    <my-ctrl:MyButton />
</ContentPage>",
                "A.B",
                "MyButton",
                "X.Y"
                );

            Assert.Contains("clr-namespace:X.Y", result);
            Assert.DoesNotContain("<my-ctrl:MyButton", result);
        }

        #endregion

        #region Pages and heads which are subjects themselves

        /// <summary>
        /// The 2009 xaml language uri of MAUI: a page which is a subject of the wizard itself
        /// (not through its code behind) gets its <c>x:Class</c> adjusted.
        /// </summary>
        [Fact]
        public async Task A_maui_page_selected_itself_gets_its_root_class_moved()
        {
            using var solution = new TestSolution()
                .AddProject(Project);

            var xamlPath = solution.AddXamlFile(Project, XamlPath,
@"<ContentPage x:Class=""Old.Views.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
</ContentPage>");

            var adjuster = new XamlAdjuster(new ClosedXamlBodyProviderFactory(), AdjustPlanItem.Xaml(xamlPath, "TestMauiApp.Views"));

            Assert.True(await adjuster.IsChangesExistsAsync());
            Assert.True(await adjuster.AdjustAsync());
            Assert.Contains(@"x:Class=""TestMauiApp.Views.MainPage""", solution.XamlTextOf(Project, XamlPath));
        }

        /// <summary>
        /// A MAUI solution has two <c>App</c> classes: the shared one and the one of the
        /// Windows head (<c>Platforms\Windows\App.xaml</c>, its own <c>x:Class</c> in the
        /// <c>WinUI</c> namespace). Moving one of them must not touch the xaml of the other.
        /// </summary>
        [Fact]
        public async Task The_app_class_of_the_windows_head_is_not_touched_by_the_shared_one()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, "App.xaml.cs",
@"namespace TestMauiApp
{
    public partial class App
    {
    }
}
")
                .AddDocument(Project, @"Platforms\Windows\App.xaml.cs",
@"namespace TestMauiApp.WinUI
{
    public partial class App
    {
    }
}
")
                ;

            var appPath = solution.AddXamlFile(Project, "App.xaml",
@"<Application x:Class=""TestMauiApp.App""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
</Application>");

            const string WindowsBody =
@"<maui:MauiWinUIApplication
    x:Class=""TestMauiApp.WinUI.App""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:maui=""using:Microsoft.Maui""
    >
    <!--  xmlns:local=""using:TestMauiApp.WinUI""  -->

</maui:MauiWinUIApplication>";

            var windowsPath = solution.AddXamlFile(Project, @"Platforms\Windows\App.xaml", WindowsBody);

            await AdjustAsync(solution, Project, "App.xaml.cs", "TestMauiApp.Core", appPath, windowsPath);

            Assert.Contains(@"x:Class=""TestMauiApp.Core.App""", solution.XamlTextOf(Project, "App.xaml"));
            Assert.Equal(WindowsBody, solution.XamlTextOf(Project, @"Platforms\Windows\App.xaml"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// Was red: the xmlns clauses nobody uses are removed by the cleanup together with the ones
        /// the move has emptied. The MAUI template declares an unused <c>xmlns:local</c> in
        /// <c>App.xaml</c>; moving <c>App</c> deletes it as a side effect, a change nobody asked
        /// for (the build is not broken by it, but the diff is bigger than the move). Only the
        /// aliases which became unused because of the move have to disappear.
        /// </summary>
        [Fact]
        public async Task An_unused_xmlns_of_the_template_survives_the_move_of_the_app_class()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, "App.xaml.cs",
@"namespace TestMauiApp
{
    public partial class App
    {
    }
}
")
                ;

            var appPath = solution.AddXamlFile(Project, "App.xaml",
@"<Application xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
             xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
             xmlns:local=""clr-namespace:TestMauiApp""
             x:Class=""TestMauiApp.App"">
</Application>");

            await AdjustAsync(solution, Project, "App.xaml.cs", "TestMauiApp.Core", appPath);

            var app = solution.XamlTextOf(Project, "App.xaml");

            Assert.Contains(@"x:Class=""TestMauiApp.Core.App""", app);
            Assert.Contains(@"xmlns:local=""clr-namespace:TestMauiApp""", app);
        }

        #endregion

        #region The implicit and the global xmlns of .NET MAUI 10

        /// <summary>
        /// Was red: with the implicit xmlns declarations of .NET MAUI 10 a page does not declare
        /// <c>xmlns:x</c> at all, the prefix is defined by the build. <c>XamlDocument</c> finds the
        /// alias of the xaml language namespace from its declaration only, so it knows no
        /// <c>x:Class</c> in such a page: the xaml keeps the old class, the code behind gets
        /// the new namespace, and the generated <c>InitializeComponent</c> no longer matches.
        /// </summary>
        [Fact]
        public async Task The_x_Class_of_a_page_with_the_implicit_x_prefix_is_moved()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Views\MainPage.xaml.cs",
@"namespace Old.Views
{
    public partial class MainPage
    {
    }
}
")
                ;

            var xamlPath = solution.AddXamlFile(Project, XamlPath,
@"<ContentPage x:Class=""Old.Views.MainPage"">
    <Label Text=""Hello"" />
</ContentPage>");

            await AdjustAsync(solution, Project, @"Views\MainPage.xaml.cs", "TestMauiApp.Views", xamlPath);

            Assert.Contains(@"x:Class=""TestMauiApp.Views.MainPage""", solution.XamlTextOf(Project, XamlPath));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// Was red: the global (and any custom) xml namespace of an assembly is a C# string:
        /// <c>[assembly: XmlnsDefinition("http://schemas.microsoft.com/dotnet/maui/global", "Old.Controls")]</c>.
        /// A page that uses the controls without a prefix (the global one), or with a prefix
        /// of the custom uri, contains no CLR namespace at all, so it needs no change, but the
        /// string in C# is not a reference for Roslyn and keeps the old namespace: the
        /// controls are not found by the xaml compiler anymore. The definition of the moved
        /// namespace has to name the target namespace.
        /// </summary>
        [Theory]
        [InlineData("http://schemas.microsoft.com/dotnet/maui/global")]
        [InlineData("http://example.com/controls")]
        public async Task An_assembly_xmlns_definition_follows_the_moved_namespace(string uri)
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Controls\MyButton.cs",
@"namespace Old.Controls
{
    public class MyButton
    {
    }
}
")
                .AddDocument(Project, @"Properties\XmlnsDefinitionAttribute.cs",
@"using System;

namespace Microsoft.Maui.Controls
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class XmlnsDefinitionAttribute : Attribute
    {
        public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace)
        {
        }
    }
}
")
                .AddDocument(Project, @"Properties\GlobalXmlns.cs",
@"using Microsoft.Maui.Controls;

[assembly: XmlnsDefinition(""" + uri + @""", ""Old.Controls"")]
")
                ;

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAsync(solution, Project, @"Controls\MyButton.cs", "TestMauiApp.Controls");

            var text = solution.TextOf(Project, @"Properties\GlobalXmlns.cs");

            Assert.Contains(@"""TestMauiApp.Controls""", text);
            Assert.DoesNotContain(@"""Old.Controls""", text);
        }

        #endregion

        #region The code generated out of a page

        /// <summary>
        /// The generated parts of a page are rewritten out of the <c>x:Class</c> on the next
        /// build, so they do not keep the old namespace alive. The paths are the ones a real
        /// build gives to the documents of the generator: under <c>obj</c>, with the
        /// <c>generated</c> folder when the project emits the generated files and without it
        /// otherwise. The generator writes two parts of a page: <c>.xaml.sg.cs</c> always and
        /// <c>.xaml.xsg.cs</c> with <c>MauiXamlInflator=SourceGen</c>.
        /// </summary>
        [Theory]
        [InlineData(@"obj\Debug\net10.0-windows10.0.19041.0\win-x64\generated\Microsoft.Maui.Controls.SourceGen\Microsoft.Maui.Controls.SourceGen.XamlGenerator\Views_MainPage.xaml.sg.cs")]
        [InlineData(@"obj\Debug\net10.0-windows10.0.19041.0\win-x64\Microsoft.Maui.Controls.SourceGen\Microsoft.Maui.Controls.SourceGen.XamlGenerator\Views_MainPage.xaml.xsg.cs")]
        public async Task The_generated_part_of_a_page_in_the_obj_folder_does_not_keep_the_old_namespace(string generatedFilePath)
        {
            var text = await MoveAPageWithAGeneratedPartAsync(generatedFilePath);

            Assert.Contains("namespace TestMauiApp.Views", text);
            Assert.DoesNotContain("using Old.Views;", text);
        }

        /// <summary>
        /// Was red: a document of a source generator in a project without an output path has
        /// the path <c>&lt;generator assembly&gt;\&lt;generator type&gt;\&lt;hint name&gt;</c>:
        /// not in <c>obj</c>, and the suffixes of the generator were not among the generated
        /// ones. The generated part of the page counted as a declaration of its own, kept
        /// <c>Old.Views</c> alive, the code behind imported it and stopped compiling (CS0234)
        /// once the generator wrote the part into the new namespace. A real build puts the
        /// documents under <c>obj</c> (see the test above), so this is the safety net for a
        /// host which does not.
        /// </summary>
        [Theory]
        [InlineData(@"Microsoft.Maui.Controls.SourceGen\Microsoft.Maui.Controls.SourceGen.XamlGenerator\Views_MainPage.xaml.sg.cs")]
        [InlineData(@"Microsoft.Maui.Controls.SourceGen\Microsoft.Maui.Controls.SourceGen.XamlGenerator\Views_MainPage.xaml.xsg.cs")]
        public async Task The_generated_part_of_a_page_outside_the_obj_folder_does_not_keep_the_old_namespace(string generatedFilePath)
        {
            var text = await MoveAPageWithAGeneratedPartAsync(generatedFilePath);

            Assert.Contains("namespace TestMauiApp.Views", text);
            Assert.DoesNotContain("using Old.Views;", text);
        }

        /// <summary>
        /// The page is moved together with its xaml; the generated partial part declares the
        /// same class in the old namespace, as the build did before the move.
        /// </summary>
        /// <returns>The text of the code behind after the adjusting.</returns>
        private static async System.Threading.Tasks.Task<string> MoveAPageWithAGeneratedPartAsync(
            string generatedFilePath
            )
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Views\MainPage.xaml.cs",
@"namespace Old.Views
{
    public partial class MainPage
    {
        public MainPage()
        {
            InitializeComponent();
        }
    }
}
")
                .AddDocument(Project, generatedFilePath,
@"namespace Old.Views
{
    public partial class MainPage
    {
        private void InitializeComponent()
        {
        }
    }
}
")
                ;

            var xamlPath = solution.AddXamlFile(Project, XamlPath,
@"<ContentPage x:Class=""Old.Views.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
</ContentPage>");

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, Project, @"Views\MainPage.xaml.cs", "TestMauiApp.Views", xamlPath);

            return solution.TextOf(Project, @"Views\MainPage.xaml.cs");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// A solution with a single project and a single C# file which declares the given
        /// members in the given namespace.
        /// </summary>
        private static TestSolution Solution(string relativeFilePath, string @namespace, string members)
        {
            return new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, relativeFilePath,
$@"namespace {@namespace}
{{
    {members}
}}
");
        }

        /// <summary>
        /// A MAUI page.
        /// </summary>
        /// <param name="rootAttributes">What follows the standard xmlns clauses in the start tag.</param>
        /// <param name="content">The content of the page.</param>
        private static string Page(string rootAttributes, string content)
        {
            return
$@"<ContentPage x:Class=""TestMauiApp.Views.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
    {rootAttributes}>
{content}
</ContentPage>";
        }

        /// <summary>
        /// Move the file into the namespace and return the text of the page after that.
        /// </summary>
        private static async System.Threading.Tasks.Task<string> MoveAsync(
            TestSolution solution,
            string relativeFilePath,
            string targetNamespace,
            string pageBody
            )
        {
            var xamlPath = solution.AddXamlFile(Project, XamlPath, pageBody);

            await AdjustAsync(solution, Project, relativeFilePath, targetNamespace, xamlPath);

            Assert.Empty(await solution.CompilationErrorsAsync());

            return solution.XamlTextOf(Project, XamlPath);
        }

        /// <summary>
        /// Move the file from <c>Old.Controls</c> into <c>TestMauiApp.Controls</c> and return the
        /// text of the page whose content is given, with the alias <c>c</c> for the old namespace.
        /// </summary>
        private static System.Threading.Tasks.Task<string> MoveIntoControlsAsync(
            TestSolution solution,
            string relativeFilePath,
            string content,
            string oldMapping = "clr-namespace:Old.Controls"
            )
        {
            return MoveAsync(
                solution,
                relativeFilePath,
                "TestMauiApp.Controls",
                Page($@"xmlns:c=""{oldMapping}""", content)
                );
        }

        /// <summary>
        /// The alias the page declares for the given namespace; there has to be exactly one.
        /// </summary>
        private static string AliasOf(string xaml, string @namespace)
        {
            var matches = Regex.Matches(
                xaml,
                @"xmlns:(\w+)=""(?:clr-namespace|using):" + Regex.Escape(@namespace) + @"[;""]"
                );

            Assert.True(matches.Count == 1, $"expected one mapping of {@namespace}, found {matches.Count}:{Environment.NewLine}{xaml}");

            return matches[0].Groups[1].Value;
        }

        #endregion
    }
}
