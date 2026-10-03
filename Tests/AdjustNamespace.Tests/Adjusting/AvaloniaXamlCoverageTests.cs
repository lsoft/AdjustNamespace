using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using System.Text.RegularExpressions;
using System.Threading;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The coverage of the Avalonia <c>.axaml</c> features which can mention a CLR namespace
    /// or a CLR type, and therefore break when a type or a namespace moves.
    /// The basic cases (<c>x:Class</c>, a <c>using:</c> xmlns, a tag) are in
    /// <see cref="AvaloniaXamlTests"/> and <c>Xaml\XamlDocumentTests</c>; this class adds what
    /// is peculiar to Avalonia: compiled bindings, the selectors with the <c>|</c> separator,
    /// the control themes, the <c>$parent[...]</c> bindings, <c>XmlnsDefinition</c> and the
    /// generated code behind.
    ///
    /// Not a subject of a test:
    /// <list type="bullet">
    /// <item><c>StyleInclude</c>/<c>ResourceInclude Source="avares://Assembly/Path"</c> is an
    /// assembly name and a resource path, never a namespace: it does not change when a type
    /// moves, see <see cref="A_resource_uri_is_not_touched"/>.</item>
    /// <item>The <c>ViewLocator</c> of the Avalonia template maps a view model to its view by
    /// replacing "ViewModel" with "View" in the full name of the type: it is a runtime string
    /// convention which the extension cannot see. What can be stated is that moving both
    /// halves keeps the convention, see <see cref="A_view_locator_convention_survives_moving_both_halves"/>.</item>
    /// </list>
    /// </summary>
    public class AvaloniaXamlCoverageTests
    {
        private const string TargetNamespace = "App.Moved";

        // ------------------------------------------------------------------
        // compiled bindings and data templates
        // ------------------------------------------------------------------

        /// <summary>
        /// <c>x:DataType</c> of a view (compiled bindings), of a <c>DataTemplate</c> and the
        /// <c>DataType</c> of a <c>TreeDataTemplate</c> are bare attribute values and follow
        /// the moved view models.
        /// </summary>
        [Fact]
        public async Task The_data_types_of_views_and_templates_follow_the_moved_view_models()
        {
            var xaml = await MoveAsync(
@"namespace Old
{
    public class MainViewModel { }
    public class ItemViewModel { }
    public class Node { }
}
",
                Axaml(
                    @"xmlns:local=""using:Old""",
@"    <Window.DataTemplates>
        <DataTemplate x:DataType=""local:ItemViewModel""><TextBlock Text=""{Binding}"" /></DataTemplate>
        <TreeDataTemplate DataType=""local:Node"" ItemsSource=""{Binding Children}""><TextBlock /></TreeDataTemplate>
    </Window.DataTemplates>",
                    @"x:DataType=""local:MainViewModel"" x:CompileBindings=""True"""
                    )
                );

            var alias = AliasOf(xaml);
            Assert.Contains($@"x:DataType=""{alias}:MainViewModel""", xaml);
            Assert.Contains($@"x:DataType=""{alias}:ItemViewModel""", xaml);
            Assert.Contains($@"DataType=""{alias}:Node""", xaml);
            Assert.DoesNotContain("using:Old", xaml);
            Assert.Equal(1, CountOf(xaml, "using:" + TargetNamespace));
        }

        /// <summary>
        /// <c>Design.DataContext</c> with a view model as a child element is an ordinary tag,
        /// the design-time attributes around it (<c>mc:Ignorable</c>, <c>d:DesignWidth</c>) are not
        /// touched.
        /// </summary>
        [Fact]
        public async Task The_design_time_data_context_follows_the_moved_view_model()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MainViewModel { } }
",
                Axaml(
                    @"xmlns:local=""using:Old"" xmlns:d=""http://schemas.microsoft.com/expression/blend/2008"" xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006""",
@"    <Design.DataContext>
        <local:MainViewModel />
    </Design.DataContext>",
                    @"mc:Ignorable=""d"" d:DesignWidth=""800"" d:DesignHeight=""450"""
                    )
                );

            var alias = AliasOf(xaml);
            Assert.Contains($"<{alias}:MainViewModel />", xaml);
            Assert.Contains(@"mc:Ignorable=""d"" d:DesignWidth=""800"" d:DesignHeight=""450""", xaml);
            Assert.DoesNotContain("using:Old", xaml);
        }

        // ------------------------------------------------------------------
        // selectors
        // ------------------------------------------------------------------

        /// <summary>
        /// Was red: the selectors of Avalonia write the namespace alias with a <c>|</c>
        /// (<c>local|MyButton</c>), not with a <c>:</c>. None of the regexes of the xaml subsystem
        /// (<c>XamlDocument.ReadControls</c>, <c>ReadTypeUsages</c>) looks for that form, so the
        /// selector keeps pointing to the alias of the old namespace, and the style silently
        /// matches nothing (or fails to parse) once the type has left it.
        /// </summary>
        [Theory]
        [InlineData("local|MyButton")]
        [InlineData("local|MyButton.accent /template/ ContentPresenter#PART_Content")]
        [InlineData("Grid > :is(local|MyButton)")]
        [InlineData("local|MyButton:pointerover")]
        public async Task A_selector_follows_the_moved_type(string selector)
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
                Axaml(
                    @"xmlns:local=""using:Old""",
$@"    <Window.Styles>
        <Style Selector=""{selector}""><Setter Property=""Width"" Value=""10"" /></Style>
    </Window.Styles>"
                    )
                );

            var alias = Regex.Match(xaml, @"(\w+)\|MyButton").Groups[1].Value;

            Assert.Contains($@"xmlns:{alias}=""using:{TargetNamespace}""", xaml);
        }

        /// <summary>
        /// Was red: the alias of a namespace is removed as soon as no <c>alias:</c> is left in the
        /// document (<c>XamlDocument.ReadUsedAliases</c>), but a selector uses it as
        /// <c>local|Other</c>. Moving <c>MyButton</c> therefore deletes the xmlns clause which
        /// the selector of the class which stays still needs.
        /// </summary>
        [Fact]
        public async Task The_xmlns_used_by_a_selector_only_is_kept()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
                Axaml(
                    @"xmlns:local=""using:Old""",
@"    <local:MyButton />
    <Window.Styles>
        <Style Selector=""local|Other""><Setter Property=""Width"" Value=""10"" /></Style>
    </Window.Styles>"
                    ),
                ("Other.cs", "namespace Old { public class Other { } }\r\n")
                );

            Assert.Contains(@"xmlns:local=""using:Old""", xaml);
            Assert.Contains(@"Selector=""local|Other""", xaml);
        }

        // ------------------------------------------------------------------
        // themes, templates, bindings, attached properties
        // ------------------------------------------------------------------

        /// <summary>
        /// A <c>ControlTheme</c> names its control in the key (<c>{x:Type}</c>) and in
        /// <c>TargetType</c>, a <c>ControlTemplate</c> in <c>TargetType</c>.
        /// </summary>
        [Fact]
        public async Task A_control_theme_and_a_control_template_follow_the_moved_control()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
                Axaml(
                    @"xmlns:local=""using:Old""",
@"    <Window.Resources>
        <ControlTheme x:Key=""{x:Type local:MyButton}"" TargetType=""local:MyButton"" BasedOn=""{StaticResource {x:Type Button}}"">
            <Setter Property=""Template"">
                <ControlTemplate TargetType=""local:MyButton""><ContentPresenter /></ControlTemplate>
            </Setter>
        </ControlTheme>
    </Window.Resources>"
                    )
                );

            var alias = AliasOf(xaml);
            Assert.Contains($@"x:Key=""{{x:Type {alias}:MyButton}}""", xaml);
            Assert.Equal(2, CountOf(xaml, $@"TargetType=""{alias}:MyButton"""));
            Assert.Contains("{StaticResource {x:Type Button}}", xaml);
            Assert.DoesNotContain("using:Old", xaml);
        }

        /// <summary>
        /// Every place of the Avalonia binding syntax where a type is written: the
        /// <c>$parent[Type]</c> and <c>$parent[Type;level]</c> paths, <c>AncestorType</c>, an
        /// attached property in the parentheses of a path, a <c>Setter</c> and a property selector.
        /// </summary>
        [Theory]
        [InlineData(@"<Button Tag=""{Binding $parent[local:MyButton].Width}"" />")]
        [InlineData(@"<Button Tag=""{Binding $parent[local:MyButton;2].Width}"" />")]
        [InlineData(@"<Button Tag=""{CompiledBinding $parent[local:MyButton].Width}"" />")]
        [InlineData(@"<Button Tag=""{Binding RelativeSource={RelativeSource AncestorType=local:MyButton}, Path=Width}"" />")]
        [InlineData(@"<Button Tag=""{Binding RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type local:MyButton}}}"" />")]
        [InlineData(@"<Button Tag=""{Binding (local:MyButton.Attached)}"" />")]
        [InlineData(@"<Button Tag=""{Binding $self.(local:MyButton.Attached)}"" />")]
        [InlineData(@"<Button local:MyButton.Attached=""True"" />")]
        [InlineData(@"<Style Selector=""Button""><Setter Property=""local:MyButton.Attached"" Value=""True"" /></Style>")]
        [InlineData(@"<Style Selector=""Button[(local:MyButton.Attached)=True]"" />")]
        [InlineData(@"<Button Tag=""{Binding Source={x:Static local:MyButton.Default}}"" />")]
        public async Task A_type_inside_a_binding_or_a_setter_follows_the_moved_type(string snippet)
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
                Axaml(@"xmlns:local=""using:Old""", "    " + snippet)
                );

            var alias = AliasOf(xaml);
            Assert.Contains(alias + ":MyButton", xaml);
            Assert.DoesNotContain("local:", xaml);
            Assert.DoesNotContain("using:Old", xaml);
        }

        /// <summary>
        /// A markup extension is written with or without the <c>Extension</c> suffix of its class,
        /// and the way the user wrote it has to survive.
        /// </summary>
        [Fact]
        public async Task A_markup_extension_follows_the_moved_class_with_and_without_the_suffix()
        {
            var xaml = await MoveAsync(
@"namespace Old
{
    public class UpperCaseExtension { }
    public class LocExtension { }
    public class Plain { }
}
",
                Axaml(
                    @"xmlns:local=""using:Old""",
@"    <TextBlock Text=""{local:UpperCase}"" />
    <TextBlock Text=""{local:LocExtension Key=Hello}"" />
    <TextBlock Text=""{local:Plain}"" />"
                    )
                );

            var alias = AliasOf(xaml);
            Assert.Contains("{" + alias + ":UpperCase}", xaml);
            Assert.Contains("{" + alias + ":LocExtension Key=Hello}", xaml);
            Assert.Contains("{" + alias + ":Plain}", xaml);
            Assert.DoesNotContain("using:Old", xaml);
        }

        /// <summary>
        /// A generic control: the tag names the generic class without its arity, and
        /// <c>x:TypeArguments</c> lists the arguments (several, separated by a comma).
        /// </summary>
        [Fact]
        public async Task A_generic_tag_and_its_type_arguments_follow_the_moved_classes()
        {
            var xaml = await MoveAsync(
@"namespace Old
{
    public class Item { }
    public class Page<T> { }
    public class Pair<T1, T2> { }
}
",
                Axaml(
                    @"xmlns:local=""using:Old""",
@"    <local:Page x:TypeArguments=""local:Item""><local:Pair x:TypeArguments=""local:Item, local:Item"" /></local:Page>"
                    )
                );

            var alias = AliasOf(xaml);
            Assert.Contains($@"<{alias}:Page x:TypeArguments=""{alias}:Item"">", xaml);
            Assert.Contains($@"<{alias}:Pair x:TypeArguments=""{alias}:Item, {alias}:Item"" />", xaml);
            Assert.Contains($"</{alias}:Page>", xaml);
            Assert.DoesNotContain("using:Old", xaml);
        }

        /// <summary>
        /// A nested type is written <c>local:Outer+Inner</c>: the outer class is the part
        /// which carries the namespace.
        /// </summary>
        [Fact]
        public async Task A_nested_type_follows_its_moved_outer_class()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class Outer { public class Inner { } } }
",
                Axaml(
                    @"xmlns:local=""using:Old""",
                    @"    <Button x:DataType=""local:Outer+Inner"" />"
                    )
                );

            var alias = AliasOf(xaml);
            Assert.Contains($@"x:DataType=""{alias}:Outer+Inner""", xaml);
            Assert.DoesNotContain("using:Old", xaml);
        }

        /// <summary>
        /// Was red: <c>CsAdjuster</c> hands every type of the file to the xaml subsystem as a pair of the
        /// namespace and the simple name, a nested type included: the nested
        /// <c>Old.Outer.Settings</c> is reported as <c>Old</c> + <c>Settings</c>, which is also the
        /// name of the top level <c>Old.Settings</c> of another file. The tag of that other class,
        /// which does not move, is rewritten to the new namespace and stops resolving.
        /// </summary>
        [Fact]
        public async Task A_nested_type_does_not_drag_a_top_level_type_of_the_same_name()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class Outer { public class Settings { } } }
",
                Axaml(
                    @"xmlns:local=""using:Old""",
                    @"    <local:Settings />"
                    ),
                ("Settings.cs", "namespace Old { public class Settings { } }\r\n")
                );

            Assert.Contains("<local:Settings />", xaml);
            Assert.Contains(@"xmlns:local=""using:Old""", xaml);
        }

        /// <summary>
        /// Every kind of a type declaration which a xaml can name: an enum and a static member of it,
        /// an interface, a record, a struct.
        /// </summary>
        [Fact]
        public async Task Every_kind_of_a_moved_type_is_followed()
        {
            var xaml = await MoveAsync(
@"namespace Old
{
    public enum Mode { Fast }
    public interface IItem { }
    public record Person { }
    public struct Point { }
}
",
                Axaml(
                    @"xmlns:local=""using:Old""",
@"    <Button Tag=""{x:Static local:Mode.Fast}"" />
    <Button x:DataType=""local:IItem"" />
    <Button Tag=""{x:Type local:Person}"" />
    <Button Tag=""{x:Type local:Point}"" />"
                    )
                );

            var alias = AliasOf(xaml);
            Assert.Contains($"{{x:Static {alias}:Mode.Fast}}", xaml);
            Assert.Contains($@"x:DataType=""{alias}:IItem""", xaml);
            Assert.Contains($"{{x:Type {alias}:Person}}", xaml);
            Assert.Contains($"{{x:Type {alias}:Point}}", xaml);
            Assert.DoesNotContain("using:Old", xaml);
        }

        // ------------------------------------------------------------------
        // the xmlns clauses
        // ------------------------------------------------------------------

        /// <summary>
        /// Avalonia accepts both <c>using:</c> and <c>clr-namespace:</c> for the very same namespace,
        /// so a document may declare it twice in two forms: both aliases are followed and a single
        /// new clause is enough for them.
        /// </summary>
        [Fact]
        public async Task A_namespace_declared_in_both_forms_is_followed()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
                Axaml(
                    @"xmlns:a=""using:Old"" xmlns:b=""clr-namespace:Old""",
@"    <a:MyButton />
    <b:MyButton />"
                    )
                );

            var alias = AliasOf(xaml);
            Assert.Equal(2, CountOf(xaml, $"<{alias}:MyButton />"));
            Assert.Equal(1, CountOf(xaml, ":" + TargetNamespace));
            Assert.DoesNotContain("using:Old", xaml);
            Assert.DoesNotContain("clr-namespace:Old", xaml);
        }

        /// <summary>
        /// An existing <c>using:</c> declaration of the target namespace is reused, no second one
        /// is added.
        /// </summary>
        [Fact]
        public async Task An_existing_using_xmlns_of_the_target_namespace_is_reused()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
                Axaml(
                    $@"xmlns:local=""using:Old"" xmlns:target=""using:{TargetNamespace}""",
@"    <local:MyButton />
    <target:Other />"
                    )
                );

            Assert.Contains("<target:MyButton />", xaml);
            Assert.Equal(1, CountOf(xaml, "using:" + TargetNamespace));
        }

        /// <summary>
        /// Was red: a mapping of the same namespace name of <i>another assembly</i>
        /// (<c>clr-namespace:Old;assembly=External</c>) is a different type, but
        /// <c>XamlControl</c> and the other performables compare the namespace only and ignore the
        /// <c>assembly=</c> suffix, so the tag of the foreign class is redirected to the namespace
        /// of this project.
        /// </summary>
        [Fact]
        public async Task A_class_of_the_same_namespace_in_another_assembly_is_not_moved()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
                Axaml(
                    @"xmlns:own=""using:Old"" xmlns:ext=""clr-namespace:Old;assembly=External""",
@"    <own:MyButton />
    <ext:MyButton />"
                    )
                );

            Assert.Contains("<ext:MyButton />", xaml);
            Assert.Contains(@"xmlns:ext=""clr-namespace:Old;assembly=External""", xaml);
        }

        /// <summary>
        /// Was red: <c>XamlDocument.ReadXmlns</c> requires the double quotes, but xml allows the
        /// single ones. A document which declares its mapping as <c>xmlns:local='using:Old'</c>
        /// has no mapping for the xaml subsystem at all, so the tag keeps the old namespace.
        /// </summary>
        [Fact]
        public async Task An_xmlns_in_single_quotes_is_followed()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
                Axaml(
                    "xmlns:local='using:Old'",
                    "    <local:MyButton />"
                    )
                );

            Assert.DoesNotContain("'using:Old'", xaml);
            Assert.DoesNotContain("<local:MyButton", xaml);
        }

        /// <summary>
        /// Was red: <c>x:Class='Old.MainWindow'</c> in single quotes is not recognized by
        /// <c>XamlDocument.ReadClasses</c> (it looks for the double quotes), so the xaml keeps
        /// the old class name while its code behind has moved: the generated partial class and the
        /// handwritten one do not match anymore.
        /// </summary>
        [Fact]
        public async Task An_x_Class_in_single_quotes_follows_its_code_behind()
        {
            using var solution = new TestSolution()
                .AddProject("App")
                .AddDocument("App", "MainWindow.axaml.cs", "namespace Old { public partial class MainWindow { } }\r\n")
                ;
            var xamlPath = solution.AddXamlFile("App", "MainWindow.axaml",
@"<Window x:Class='Old.MainWindow'
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            await AdjustAsync(solution, "App", "MainWindow.axaml.cs", TargetNamespace, xamlPath);

            var xaml = solution.XamlTextOf("App", "MainWindow.axaml");
            Assert.Contains(TargetNamespace + ".MainWindow", xaml);
            Assert.DoesNotContain("Old.MainWindow", xaml);
        }

        /// <summary>
        /// Was red: a new xmlns clause is written behind the <i>last</i> xmlns of the document
        /// (<c>XamlDocument.MoveObject</c>), even when that one is declared on a nested element.
        /// Its scope is that element only, so the root level tag which uses the new alias is
        /// outside of the scope.
        /// </summary>
        [Fact]
        public async Task A_new_xmlns_is_declared_on_the_root_and_not_on_a_nested_element()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
@"<Window x:Class=""App.Views.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""using:Old"">
    <local:MyButton />
    <Grid xmlns:sys=""using:System"">
        <sys:String x:Key=""s"">text</sys:String>
    </Grid>
</Window>"
                );

            var rootTagEnd = xaml.IndexOf('>');

            Assert.InRange(xaml.IndexOf("using:" + TargetNamespace, StringComparison.Ordinal), 0, rootTagEnd);
        }

        /// <summary>
        /// The resource uris of <c>StyleInclude</c>/<c>ResourceInclude</c> carry an assembly name and
        /// a path, never a namespace: they are not touched while a type moves.
        /// </summary>
        [Fact]
        public async Task A_resource_uri_is_not_touched()
        {
            var xaml = await MoveAsync(
@"namespace Old { public class MyButton { } }
",
                Axaml(
                    @"xmlns:local=""using:Old""",
@"    <Window.Styles>
        <StyleInclude Source=""avares://Old/Styles/Old.MyButton.axaml"" />
        <StyleInclude Source=""avares://Avalonia.Themes.Fluent/Controls/Button.axaml"" />
    </Window.Styles>
    <local:MyButton />"
                    )
                );

            Assert.Contains(@"Source=""avares://Old/Styles/Old.MyButton.axaml""", xaml);
            Assert.Contains(@"Source=""avares://Avalonia.Themes.Fluent/Controls/Button.axaml""", xaml);
            Assert.DoesNotContain("using:Old", xaml);
        }

        // ------------------------------------------------------------------
        // XmlnsDefinition
        // ------------------------------------------------------------------

        /// <summary>
        /// Was red: a library maps its namespace to a xml namespace with
        /// <c>[assembly: XmlnsDefinition("https://my.lib/ui", "Old.Controls")]</c>, and the xaml writes
        /// <c>&lt;ui:MyButton/&gt;</c> with <c>xmlns:ui="https://my.lib/ui"</c>. A string is not a
        /// reference for Roslyn and an alias which is not a clr-namespace one is skipped by the xaml
        /// subsystem, so after the move the attribute still exports the (now empty) old namespace and
        /// every <c>&lt;ui:MyButton/&gt;</c> stops resolving at runtime. The wanted behaviour is that
        /// the attribute follows the namespace of the type.
        /// </summary>
        [Fact]
        public async Task The_XmlnsDefinition_attribute_follows_the_moved_namespace()
        {
            using var solution = new TestSolution()
                .AddProject("App")
                .AddDocument("App", "AvaloniaStub.cs",
@"namespace Avalonia.Metadata
{
    [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class XmlnsDefinitionAttribute : System.Attribute
    {
        public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace) { }
    }
}
")
                .AddDocument("App", "AssemblyInfo.cs",
@"using Avalonia.Metadata;

[assembly: XmlnsDefinition(""https://my.lib/ui"", ""Old.Controls"")]
")
                .AddDocument("App", @"Controls\MyButton.cs",
@"namespace Old.Controls
{
    public class MyButton { }
}
")
                ;
            var xamlPath = solution.AddXamlFile("App", "MainWindow.axaml",
@"<Window x:Class=""App.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:ui=""https://my.lib/ui"">
    <ui:MyButton />
</Window>");
            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAsync(solution, "App", @"Controls\MyButton.cs", "App.Widgets", xamlPath);

            Assert.Contains(@"""App.Widgets""", solution.TextOf("App", "AssemblyInfo.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        // ------------------------------------------------------------------
        // the whole application: App.axaml, views, code behind
        // ------------------------------------------------------------------

        /// <summary>
        /// <c>App.axaml</c> with the <c>ViewLocator</c> in <c>Application.DataTemplates</c>: the
        /// root class of the application and the locator are moved one after another.
        /// </summary>
        [Fact]
        public async Task The_application_and_its_view_locator_are_followed()
        {
            using var solution = new TestSolution()
                .AddProject("Sample")
                .AddDocument("Sample", "App.axaml.cs", "namespace Old { public partial class App { } }\r\n")
                .AddDocument("Sample", "ViewLocator.cs", "namespace Old { public class ViewLocator { } }\r\n")
                ;
            var xamlPath = solution.AddXamlFile("Sample", "App.axaml",
@"<Application x:Class=""Old.App""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""using:Old"">
    <Application.DataTemplates>
        <local:ViewLocator />
    </Application.DataTemplates>
</Application>");
            Assert.Empty(await solution.CompilationErrorsAsync());

            var center = await AdjustAsync(solution, "Sample", "ViewLocator.cs", "Sample.Services", xamlPath);
            await AdjustAsync(solution, center, "Sample", "App.axaml.cs", "Sample", xamlPath);
            await CleanupAsync(solution, center);

            var xaml = solution.XamlTextOf("Sample", "App.axaml");
            var locatorAlias = AliasOf(xaml, "Sample.Services");
            Assert.Contains(@"x:Class=""Sample.App""", xaml);
            Assert.Contains($"<{locatorAlias}:ViewLocator />", xaml);
            Assert.DoesNotContain("using:Old", xaml);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// A view which is hosted by a window: the view moves with its code behind, its own
        /// <c>x:Class</c> and the tag in the xaml of the window follow it.
        /// </summary>
        [Fact]
        public async Task A_moved_view_is_followed_by_its_own_xaml_and_by_the_window_which_hosts_it()
        {
            using var solution = new TestSolution()
                .AddProject("Sample")
                .AddDocument("Sample", @"Views\MainView.axaml.cs", "namespace Old { public partial class MainView { } }\r\n")
                .AddDocument("Sample", "MainWindow.axaml.cs", "namespace Old { public partial class MainWindow { } }\r\n")
                ;
            var viewPath = solution.AddXamlFile("Sample", @"Views\MainView.axaml",
@"<UserControl x:Class=""Old.MainView""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>");
            var windowPath = solution.AddXamlFile("Sample", "MainWindow.axaml",
@"<Window x:Class=""Old.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:views=""using:Old"">
    <views:MainView />
</Window>");

            await AdjustAsync(solution, "Sample", @"Views\MainView.axaml.cs", "Sample.Views", viewPath, windowPath);

            Assert.Contains(@"x:Class=""Sample.Views.MainView""", solution.XamlTextOf("Sample", @"Views\MainView.axaml"));

            var window = solution.XamlTextOf("Sample", "MainWindow.axaml");
            var alias = AliasOf(window, "Sample.Views");
            Assert.Contains($"<{alias}:MainView />", window);
            Assert.Contains(@"x:Class=""Old.MainWindow""", window);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The <c>ViewLocator</c> of the Avalonia template maps a view model to its view by replacing
        /// "ViewModel" with "View" in the full name of the type. The extension cannot see that string
        /// convention; what can be stated is that a session which moves both halves into the namespaces
        /// of their folders (<c>ViewModels</c> / <c>Views</c>) leaves the convention intact. Moving
        /// only one half would break it at runtime and no test can state that without running the
        /// application.
        /// </summary>
        [Fact]
        public async Task A_view_locator_convention_survives_moving_both_halves()
        {
            using var solution = new TestSolution()
                .AddProject("Sample")
                .AddDocument("Sample", @"ViewModels\MainViewModel.cs", "namespace Sample { public class MainViewModel { } }\r\n")
                .AddDocument("Sample", @"Views\MainView.axaml.cs", "namespace Sample { public partial class MainView { } }\r\n")
                .AddDocument("Sample", "ViewLocator.cs",
@"namespace Sample
{
    public class ViewLocator
    {
        public string Locate(object viewModel) => viewModel.GetType().FullName.Replace(""ViewModel"", ""View"");
    }
}
")
                ;
            var viewPath = solution.AddXamlFile("Sample", @"Views\MainView.axaml",
@"<UserControl x:Class=""Sample.MainView""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>");

            var outcome = await new AdjustSession(solution.Context, new NamespaceReplaceRegex(string.Empty, string.Empty))
                .RunAsync(
                    new[]
                    {
                        solution.PathOf("Sample", @"ViewModels\MainViewModel.cs"),
                        solution.PathOf("Sample", @"Views\MainView.axaml.cs"),
                        viewPath
                    },
                    null,
                    CancellationToken.None
                    );

            Assert.Equal(AdjustSessionOutcome.Completed, outcome);

            var viewModel = await solution.GetTypeAsync("Sample", "Sample.ViewModels.MainViewModel");
            var viewName = viewModel.ToDisplayString().Replace("ViewModel", "View");

            //the type the locator would ask for exists, and the xaml names the same class
            await solution.GetTypeAsync("Sample", viewName);
            Assert.Contains($@"x:Class=""{viewName}""", solution.XamlTextOf("Sample", @"Views\MainView.axaml"));
            Assert.Contains(@"""ViewModel"", ""View""", solution.TextOf("Sample", "ViewLocator.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// Was red: a code behind class in the global namespace (no <c>namespace</c> at all) is not moved by
        /// the extension (<c>AdjustPlanner</c> finds no namespace transition in the .cs file), but
        /// <c>AdjustPlanner.PlanXaml</c> plans the .axaml unconditionally and <c>XamlAdjuster</c> rewrites
        /// its <c>x:Class="MainView"</c> into <c>Sample.Views.MainView</c>. The two halves of the class
        /// name different types after the run (the xaml one does not exist), and the next build fails.
        /// The wanted behaviour is that both halves stay consistent: either both are moved or none.
        /// </summary>
        [Fact]
        public async Task A_view_in_the_global_namespace_keeps_its_two_halves_consistent()
        {
            using var solution = new TestSolution()
                .AddProject("Sample")
                .AddDocument("Sample", @"Views\MainView.axaml.cs", "public partial class MainView { }\r\n")
                ;
            var viewPath = solution.AddXamlFile("Sample", @"Views\MainView.axaml",
@"<UserControl x:Class=""MainView""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>");

            await new AdjustSession(solution.Context, new NamespaceReplaceRegex(string.Empty, string.Empty))
                .RunAsync(
                    new[]
                    {
                        solution.PathOf("Sample", @"Views\MainView.axaml.cs"),
                        viewPath
                    },
                    null,
                    CancellationToken.None
                    );

            var xamlClass = Regex.Match(solution.XamlTextOf("Sample", @"Views\MainView.axaml"), @"x:Class=""([^""]+)""").Groups[1].Value;

            //throws if there is no such type
            await solution.GetTypeAsync("Sample", xamlClass);
        }

        /// <summary>
        /// The Avalonia source generator emits the other part of the code behind
        /// (<c>InitializeComponent</c>, the fields of the named controls) out of <c>x:Class</c>.
        /// That part follows the xaml the adjusting has just changed, so it neither keeps the old
        /// namespace alive (no <c>using Old;</c> in the moved file) nor blocks the move.
        /// </summary>
        [Fact]
        public async Task The_old_namespace_of_a_generated_avalonia_part_is_not_imported()
        {
            const string GeneratedFilePath = @"obj\Debug\net8.0\generated\Avalonia.Generators\MainWindow.axaml.g.cs";

            using var solution = new TestSolution()
                .AddProject("Sample")
                .AddDocument("Sample", @"Views\MainWindow.axaml.cs",
@"namespace Old
{
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
")
                .AddDocument("Sample", GeneratedFilePath,
@"namespace Old
{
    public partial class MainWindow
    {
        public void InitializeComponent() { }
    }
}
")
                ;
            var xamlPath = solution.AddXamlFile("Sample", @"Views\MainWindow.axaml",
@"<Window x:Class=""Old.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");
            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, "Sample", @"Views\MainWindow.axaml.cs", "Sample.Views", xamlPath);

            var text = solution.TextOf("Sample", @"Views\MainWindow.axaml.cs");
            Assert.Contains("namespace Sample.Views", text);
            Assert.DoesNotContain("using Old;", text);
            Assert.Contains(@"x:Class=""Sample.Views.MainWindow""", solution.XamlTextOf("Sample", @"Views\MainWindow.axaml"));

            //the next build regenerates the part out of the new x:Class
            solution.ReplaceDocument("Sample", GeneratedFilePath,
@"namespace Sample.Views
{
    public partial class MainWindow
    {
        public void InitializeComponent() { }
    }
}
");
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        // ------------------------------------------------------------------
        // the file itself
        // ------------------------------------------------------------------

        /// <summary>
        /// The code behind of an <c>.axaml</c> is the file with <c>.cs</c> appended to its name, so an
        /// <c>.axaml</c> of a code behind which several projects compile is blocked as a <c>.xaml</c>
        /// is.
        /// </summary>
        [Fact]
        public async Task An_axaml_file_of_a_skipped_code_behind_is_blocked()
        {
            using var solution = new TestSolution()
                .AddProject("A")
                .AddProject("B")
                .AddSharedProject("Common")
                .AddSharedDocument("Common", "MainWindow.axaml.cs",
@"namespace Legacy.Core
{
    public partial class MainWindow { }
}
", "A", "B")
                ;
            var xamlPath = solution.AddXamlFile("Common", "MainWindow.axaml",
@"<Window x:Class=""Legacy.Core.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            var result = await AdjustPlanner.PlanAsync(solution.Workspace, xamlPath, "Common");

            Assert.True(result.HasBlock);
            Assert.Equal(AdjustBlockKind.XamlCodeBehindMultiProject, result.Block!.Value.Kind);
        }

        /// <summary>
        /// The extension of the file is compared without the case.
        /// </summary>
        [Fact]
        public async Task An_axaml_file_with_an_upper_case_extension_is_processed()
        {
            using var solution = new TestSolution()
                .AddProject("App")
                .AddDocument("App", "MyButton.cs", "namespace Old { public class MyButton { } }\r\n")
                ;
            var xamlPath = solution.AddXamlFile("App", "MainWindow.AXAML",
                Axaml(@"xmlns:local=""using:Old""", "    <local:MyButton />")
                );

            await AdjustAsync(solution, "App", "MyButton.cs", TargetNamespace, xamlPath);

            var xaml = solution.XamlTextOf("App", "MainWindow.AXAML");
            Assert.DoesNotContain("using:Old", xaml);
            Assert.Contains("using:" + TargetNamespace, xaml);
        }

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// A window with the given xmlns mappings, root attributes and content.
        /// </summary>
        private static string Axaml(string mappings, string content, string rootAttributes = "")
        {
            return
$@"<Window x:Class=""App.Views.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    {mappings}
    {rootAttributes}>
{content}
</Window>";
        }

        /// <summary>
        /// Move the types of <paramref name="csBody"/> (file <c>Moved.cs</c> of the project <c>App</c>)
        /// into <see cref="TargetNamespace"/> and return the adjusted <c>Views\MainWindow.axaml</c>.
        /// The solution has to compile before and after.
        /// </summary>
        private static async System.Threading.Tasks.Task<string> MoveAsync(
            string csBody,
            string axaml,
            params (string Path, string Body)[] otherFiles
            )
        {
            using var solution = new TestSolution()
                .AddProject("App")
                .AddDocument("App", "Moved.cs", csBody)
                ;
            foreach (var (path, body) in otherFiles)
            {
                solution.AddDocument("App", path, body);
            }

            var xamlPath = solution.AddXamlFile("App", @"Views\MainWindow.axaml", axaml);
            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAsync(solution, "App", "Moved.cs", TargetNamespace, xamlPath);

            Assert.Empty(await solution.CompilationErrorsAsync());

            return solution.XamlTextOf("App", @"Views\MainWindow.axaml");
        }

        /// <summary>
        /// The alias the document declares for the namespace (a new one is generated, so its name
        /// is not known in advance).
        /// </summary>
        private static string AliasOf(string xaml, string? @namespace = null)
        {
            var match = Regex.Match(
                xaml,
                @"xmlns:(\w+)=""(?:using|clr-namespace):" + Regex.Escape(@namespace ?? TargetNamespace) + @"[""; ]"
                );

            Assert.True(match.Success, "There is no xmlns clause of the namespace in:\r\n" + xaml);

            return match.Groups[1].Value;
        }
    }
}
