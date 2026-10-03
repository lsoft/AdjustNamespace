using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static AdjustNamespace.Tests.Infrastructure.AdjustRunner;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// The WPF xaml features which can mention a CLR namespace or a CLR type and therefore
    /// break when a type or a namespace moves, checked end to end: a C# file is moved, the
    /// xaml of the solution is read back from the disk and the C# part is compiled.
    ///
    /// The xaml itself is no part of the compilation here, so every result is additionally
    /// parsed as xml: a prefix which is used but no more declared (a lost or misplaced
    /// <c>xmlns</c>) is an error of the parser.
    ///
    /// What the plain string level covers is in <c>Xaml\XamlDocumentTests</c> and
    /// <c>Xaml\XamlReferenceKindTests</c> (tags, <c>x:Type</c>, <c>x:Static</c>, bare attribute
    /// values, attached properties, markup extensions, <c>x:TypeArguments</c>, the assembly
    /// suffix, comments), the neighbours of this file are <c>XamlAdjusterTests</c>,
    /// <c>GeneratedCodeBehindTests</c>, <c>MauiXamlTests</c> and <c>AvaloniaXamlTests</c>.
    /// </summary>
    public class WpfXamlTests
    {
        private const string Project = "MyApp";

        private const string ItemBody =
@"namespace Old.Models
{
    public class Item
    {
        public static Item Default;
    }
}
";

        #region Typed references of resources, templates and bindings

        /// <summary>
        /// <c>DataType</c>, <c>TargetType</c>, <c>BasedOn</c> and a key by type: every place a
        /// style or a template names the class it is made for.
        /// </summary>
        [Fact]
        public async Task The_types_of_styles_and_templates_follow_the_moved_class()
        {
            var xaml = await MoveAsync(ItemBody, Host("Old.Models",
@"    <UserControl.Resources>
        <DataTemplate DataType=""{x:Type local:Item}"">
            <TextBlock Text=""{Binding}"" />
        </DataTemplate>
        <HierarchicalDataTemplate DataType=""{x:Type local:Item}"" />
        <ControlTemplate x:Key=""t"" TargetType=""{x:Type local:Item}"" />
        <Style x:Key=""{x:Type local:Item}"" TargetType=""local:Item"" />
        <Style x:Key=""derived"" TargetType=""local:Item"" BasedOn=""{StaticResource {x:Type local:Item}}"" />
    </UserControl.Resources>"),
                "MyApp.Models");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Equal(7, CountOf(xaml, alias + ":Item"));
            Assert.Contains(@"BasedOn=""{StaticResource {x:Type " + alias + @":Item}}""", xaml);
        }

        /// <summary>
        /// The design time data context is written in two ways, with and without
        /// <c>Type=</c>, and may take the type through <c>x:Type</c>. The <c>d:</c> and
        /// <c>mc:</c> prefixes are no CLR mappings and must stay.
        /// </summary>
        [Fact]
        public async Task The_design_instances_follow_the_moved_class()
        {
            var xaml = await MoveAsync(ItemBody, Host("Old.Models",
@"    <Grid d:DataContext=""{d:DesignInstance Type=local:Item, IsDesignTimeCreatable=True}"">
        <ListBox d:DataContext=""{d:DesignInstance local:Item}"" />
        <ListBox d:DataContext=""{d:DesignInstance Type={x:Type local:Item}}"" />
    </Grid>"),
                "MyApp.Models");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Equal(3, CountOf(xaml, alias + ":Item"));
            Assert.Contains(@"{d:DesignInstance Type=" + alias + @":Item, IsDesignTimeCreatable=True}", xaml);
            Assert.Contains(@"mc:Ignorable=""d""", xaml);
            Assert.Contains(@"xmlns:d=""http://schemas.microsoft.com/expression/blend/2008""", xaml);
        }

        /// <summary>
        /// <c>ObjectType</c> is a <c>Type</c>, so it is written both as <c>{x:Type}</c> and as
        /// a bare value; <c>ObjectInstance</c> takes an <c>{x:Static}</c>.
        /// </summary>
        [Fact]
        public async Task The_object_data_providers_follow_the_moved_class()
        {
            var xaml = await MoveAsync(ItemBody, Host("Old.Models",
@"    <UserControl.Resources>
        <ObjectDataProvider x:Key=""p1"" ObjectType=""{x:Type local:Item}"" MethodName=""Create"" />
        <ObjectDataProvider x:Key=""p2"" ObjectInstance=""{x:Static local:Item.Default}"" />
        <ObjectDataProvider x:Key=""p3"" ObjectType=""local:Item"" />
    </UserControl.Resources>"),
                "MyApp.Models");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Contains(@"ObjectType=""{x:Type " + alias + @":Item}""", xaml);
            Assert.Contains(@"ObjectInstance=""{x:Static " + alias + @":Item.Default}""", xaml);
            Assert.Contains(@"ObjectType=""" + alias + @":Item""", xaml);
        }

        /// <summary>
        /// The xaml language elements in their object element form carry the class in an
        /// attribute: <c>TypeName</c> of <c>x:Type</c>, <c>Member</c> of <c>x:Static</c> and
        /// <c>Type</c> of <c>x:Array</c>.
        /// </summary>
        [Fact]
        public async Task The_xaml_language_elements_follow_the_moved_class()
        {
            var xaml = await MoveAsync(ItemBody, Host("Old.Models",
@"    <UserControl.Resources>
        <x:Type x:Key=""t"" TypeName=""local:Item"" />
        <x:Static x:Key=""s"" Member=""local:Item.Default"" />
        <x:Array x:Key=""a"" Type=""local:Item"" />
    </UserControl.Resources>"),
                "MyApp.Models");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Contains(@"TypeName=""" + alias + @":Item""", xaml);
            Assert.Contains(@"Member=""" + alias + @":Item.Default""", xaml);
            Assert.Contains(@"Type=""" + alias + @":Item""", xaml);
        }

        /// <summary>
        /// A component resource key names the type of the assembly the resource lives in.
        /// </summary>
        [Fact]
        public async Task The_component_resource_keys_follow_the_moved_class()
        {
            var xaml = await MoveAsync(ItemBody, Host("Old.Models",
@"    <Grid>
        <Grid.Resources>
            <SolidColorBrush x:Key=""{ComponentResourceKey TypeInTargetAssembly={x:Type local:Item}, ResourceId=Brush}"" />
        </Grid.Resources>
        <Border Background=""{DynamicResource {ComponentResourceKey TypeInTargetAssembly={x:Type local:Item}, ResourceId=Brush}}"" />
        <Border Background=""{DynamicResource {ComponentResourceKey TypeInTargetAssembly=local:Item, ResourceId=Brush}}"" />
        <Border Background=""{DynamicResource {x:Static local:Item.Default}}"" />
    </Grid>"),
                "MyApp.Models");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Equal(4, CountOf(xaml, alias + ":Item"));
        }

        /// <summary>
        /// <c>AncestorType</c> takes the class with and without <c>{x:Type}</c>.
        /// </summary>
        [Fact]
        public async Task The_relative_sources_follow_the_moved_class()
        {
            var xaml = await MoveAsync(ItemBody, Host("Old.Models",
@"    <StackPanel>
        <TextBlock Text=""{Binding RelativeSource={RelativeSource AncestorType={x:Type local:Item}}, Path=Name}"" />
        <TextBlock Text=""{Binding Name, RelativeSource={RelativeSource Mode=FindAncestor, AncestorType=local:Item}}"" />
        <TextBlock Text=""{Binding Path=Name, RelativeSource={RelativeSource FindAncestor, AncestorType=local:Item, AncestorLevel=2}}"" />
    </StackPanel>"),
                "MyApp.Models");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Equal(3, CountOf(xaml, alias + ":Item"));
        }

        /// <summary>
        /// An attached property of the moved class is named in an attribute, in a property
        /// element, in a binding path, in a setter, in a trigger and in a storyboard target.
        /// </summary>
        [Fact]
        public async Task The_attached_properties_follow_the_moved_class()
        {
            var xaml = await MoveAsync(ItemBody, Host("Old.Models",
@"    <StackPanel>
        <Button local:Item.Count=""1"" Content=""{Binding Path=(local:Item.Count), RelativeSource={RelativeSource Self}}"" />
        <Button Content=""{Binding (local:Item.Count), RelativeSource={RelativeSource Self}}"">
            <local:Item.Count>2</local:Item.Count>
        </Button>
        <StackPanel.Resources>
            <Style TargetType=""Button"">
                <Setter Property=""local:Item.Count"" Value=""3"" />
                <Style.Triggers>
                    <Trigger Property=""local:Item.Count"" Value=""4"" />
                </Style.Triggers>
            </Style>
            <Storyboard x:Key=""s"">
                <DoubleAnimation Storyboard.TargetProperty=""(local:Item.Count)"" />
            </Storyboard>
        </StackPanel.Resources>
    </StackPanel>"),
                "MyApp.Models");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Equal(8, CountOf(xaml, alias + ":Item"));
            Assert.Contains("<" + alias + ":Item.Count>2</" + alias + ":Item.Count>", xaml);
        }

        #endregion

        #region Markup extensions

        /// <summary>
        /// The class of a markup extension is named <c>UpperCaseExtension</c> and written in a
        /// xaml as <c>{local:UpperCase}</c> as well as <c>{local:UpperCaseExtension}</c>, also
        /// as an argument of another extension. The user's spelling is kept.
        /// </summary>
        [Fact]
        public async Task A_markup_extension_named_with_the_suffix_follows_the_moved_class()
        {
            var xaml = await MoveAsync(
@"namespace Old.Models
{
    public class UpperCaseExtension
    {
        public string Text;
    }
}
",
                Host("Old.Models",
@"    <StackPanel>
        <TextBlock Text=""{local:UpperCase abc}"" />
        <TextBlock Text=""{local:UpperCaseExtension abc}"" />
        <TextBlock Text=""{local:UpperCase Text=abc}"" />
        <TextBlock Text=""{Binding Name, ConverterParameter={local:UpperCase Text=x}}"" />
    </StackPanel>"),
                "MyApp.Models",
                @"Models\UpperCaseExtension.cs");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Contains("{" + alias + ":UpperCase abc}", xaml);
            Assert.Contains("{" + alias + ":UpperCaseExtension abc}", xaml);
            Assert.Contains("{" + alias + ":UpperCase Text=abc}", xaml);
            Assert.Contains("ConverterParameter={" + alias + ":UpperCase Text=x}", xaml);
        }

        /// <summary>
        /// Was red: a markup extension with a complex argument is written as an object element:
        /// <c>&lt;local:UpperCase/&gt;</c> is the class <c>UpperCaseExtension</c> (the suffix
        /// is optional for an element as well as for the curly braces syntax). Only
        /// <c>XamlTypeUsage</c> knows the suffix and only inside of the curly braces;
        /// <c>XamlControl.Perform</c> compares the name of a tag with the name of the class
        /// as it is, so the element keeps pointing to the namespace the class has left.
        /// </summary>
        [Fact]
        public async Task A_markup_extension_written_as_an_element_without_the_suffix_follows_the_moved_class()
        {
            var xaml = await MoveAsync(
@"namespace Old.Models
{
    public class UpperCaseExtension
    {
        public string Text;
    }
}
",
                Host("Old.Models",
@"    <TextBlock>
        <TextBlock.Text>
            <local:UpperCase Text=""abc"" />
        </TextBlock.Text>
    </TextBlock>"),
                "MyApp.Models",
                @"Models\UpperCaseExtension.cs");

            Assert.DoesNotContain("local:", xaml);
            Assert.DoesNotContain("Old.Models", xaml);
        }

        #endregion

        #region The root element and the generics

        /// <summary>
        /// The base class of a page is the root element of its xaml, with the generic argument
        /// on the same tag and the property elements of it; three moved types share a single
        /// new mapping.
        /// </summary>
        [Fact]
        public async Task A_generic_base_class_of_the_root_element_follows_the_moved_classes()
        {
            var xaml = await MoveAsync(
@"namespace Old.Models
{
    public class Item { }

    public class Pair { }

    public class BasePage<T> { }
}
",
@"<local:BasePage x:Class=""Q.W.Host"" x:TypeArguments=""local:Item""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:collections=""clr-namespace:System.Collections.Generic;assembly=mscorlib""
    xmlns:local=""clr-namespace:Old.Models"">
    <local:BasePage.Resources>
        <collections:List x:TypeArguments=""local:Item, local:Pair"" />
    </local:BasePage.Resources>
</local:BasePage>",
                "MyApp.Models",
                @"Models\Item.cs");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Equal(4, CountOf(xaml, alias + ":BasePage"));
            Assert.Equal(2, CountOf(xaml, alias + ":Item"));
            Assert.Equal(1, CountOf(xaml, alias + ":Pair"));
            Assert.Equal(1, CountOf(xaml, "clr-namespace:MyApp.Models"));
            Assert.Contains("clr-namespace:System.Collections.Generic;assembly=mscorlib", xaml);
        }

        /// <summary>
        /// A nested class is written as <c>local:Outer+Inner</c> (not in a tag name, where
        /// <c>+</c> is not allowed): the part behind the plus survives.
        /// </summary>
        [Fact]
        public async Task A_nested_class_follows_its_outer_class()
        {
            var xaml = await MoveAsync(
@"namespace Old.Models
{
    public class Outer
    {
        public class Inner
        {
            public static Inner Default;
        }
    }
}
",
                Host("Old.Models",
@"    <Grid.Resources>
        <Style TargetType=""{x:Type local:Outer}"" />
        <ObjectDataProvider ObjectType=""{x:Type local:Outer+Inner}"" />
        <DataTemplate DataType=""{x:Type local:Outer+Inner}"" />
        <Style TargetType=""local:Outer+Inner"" />
        <x:Static x:Key=""s"" Member=""local:Outer+Inner.Default"" />
    </Grid.Resources>"),
                "MyApp.Models",
                @"Models\Outer.cs");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Equal(5, CountOf(xaml, alias + ":Outer"));
            Assert.Equal(4, CountOf(xaml, alias + ":Outer+Inner"));
        }

        /// <summary>
        /// Was red: the nested types are queued for the xaml fix as if they were top level ones
        /// (<c>CsAdjuster.PerformChanges</c> calls <c>XamlDocument.MoveObject</c> with
        /// <c>pair.Key.Name</c> and the namespace of the nested symbol), so the nested
        /// <c>Outer.Item</c> drags the references to the unrelated top level
        /// <c>Old.Models.Item</c>, which stays where it is, into the new namespace.
        /// </summary>
        [Fact]
        public async Task A_nested_class_does_not_move_the_unrelated_class_of_the_same_name()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Models\Outer.cs",
@"namespace Old.Models
{
    public class Outer
    {
        public class Item { }
    }
}
")
                .AddDocument(Project, @"Models\Item.cs",
@"namespace Old.Models
{
    public class Item { }
}
");

            solution.AddXamlFile(Project, "Host.xaml", Host("Old.Models", @"    <local:Item />"));

            await MoveAsync(solution, @"Models\Outer.cs", "MyApp.Models");

            var xaml = solution.XamlTextOf(Project, "Host.xaml");

            Assert.Contains("<local:Item />", xaml);
            Assert.Contains("clr-namespace:Old.Models", xaml);
        }

        #endregion

        #region Themes, resource dictionaries and the application

        /// <summary>
        /// <c>Themes\Generic.xaml</c> of a custom control has no <c>x:Class</c> at all and
        /// names the control twice: for the style and for the template.
        /// </summary>
        [Fact]
        public async Task The_generic_theme_of_a_custom_control_follows_the_moved_class()
        {
            var xaml = await MoveAsync(
@"namespace Old.Controls
{
    public class CustomControl { }
}
",
@"<ResourceDictionary
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:Old.Controls"">
    <Style TargetType=""{x:Type local:CustomControl}"">
        <Setter Property=""Template"">
            <Setter.Value>
                <ControlTemplate TargetType=""{x:Type local:CustomControl}"">
                    <Border Background=""{TemplateBinding Background}"" />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ResourceDictionary>",
                "MyApp.Controls",
                @"Controls\CustomControl.cs",
                @"Themes\Generic.xaml");

            var alias = AliasOf(xaml, "MyApp.Controls");

            Assert.DoesNotContain("local:", xaml);
            Assert.Equal(2, CountOf(xaml, alias + ":CustomControl"));
            Assert.Contains("{TemplateBinding Background}", xaml);
        }

        /// <summary>
        /// A resource dictionary with a code behind has an <c>x:Class</c> as a window has.
        /// </summary>
        [Fact]
        public async Task The_x_Class_of_a_resource_dictionary_follows_its_code_behind()
        {
            var xaml = await MoveAsync(
@"namespace Old.Dicts
{
    public partial class Styles { }
}
",
@"<ResourceDictionary x:Class=""Old.Dicts.Styles""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Style x:Key=""s"" TargetType=""Button"" />
</ResourceDictionary>",
                "MyApp.Dicts",
                @"Dicts\Styles.xaml.cs",
                @"Dicts\Styles.xaml");

            Assert.Contains(@"x:Class=""MyApp.Dicts.Styles""", xaml);
            Assert.Contains(@"<Style x:Key=""s"" TargetType=""Button"" />", xaml);
        }

        /// <summary>
        /// <c>App.xaml</c>: the class of the application is moved and the paths to the other
        /// xaml files (<c>StartupUri</c>, <c>Source</c>) are not namespaces at all.
        /// </summary>
        [Fact]
        public async Task The_application_class_is_moved_and_the_paths_are_not_touched()
        {
            const string Body =
@"<Application x:Class=""Old.App""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    StartupUri=""MainWindow.xaml"">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source=""pack://application:,,,/MyApp;component/Themes/Generic.xaml"" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>";

            var xaml = await MoveAsync(
@"namespace Old
{
    public partial class App { }
}
",
                Body,
                "MyApp",
                "App.xaml.cs",
                "App.xaml");

            Assert.Equal(Body.Replace("Old.App", "MyApp.App"), xaml);
        }

        #endregion

        #region The mappings

        /// <summary>
        /// The mapping of a namespace which keeps other types stays, and the moved class gets
        /// a mapping of its own.
        /// </summary>
        [Fact]
        public async Task The_mapping_of_a_namespace_which_keeps_other_types_stays()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Controls\MyButton.cs",
@"namespace Old.Controls
{
    public class MyButton { }
}
")
                .AddDocument(Project, @"Controls\MyLabel.cs",
@"namespace Old.Controls
{
    public class MyLabel { }
}
");

            solution.AddXamlFile(Project, "Host.xaml", Host("Old.Controls",
@"    <StackPanel>
        <local:MyButton />
        <local:MyLabel />
    </StackPanel>"));

            await MoveAsync(solution, @"Controls\MyButton.cs", "MyApp.Controls");

            var xaml = solution.XamlTextOf(Project, "Host.xaml");
            var alias = AliasOf(xaml, "MyApp.Controls");

            Assert.Contains("<" + alias + ":MyButton />", xaml);
            Assert.Contains("<local:MyLabel />", xaml);
            Assert.Contains(@"xmlns:local=""clr-namespace:Old.Controls""", xaml);
        }

        /// <summary>
        /// A namespace of the project mapped twice (the second time with the explicit own
        /// assembly) gets two mappings for the target, each one keeps its own form.
        /// </summary>
        [Fact]
        public async Task Two_mappings_of_the_same_namespace_are_both_followed()
        {
            var xaml = await MoveAsync(
@"namespace Old.Controls
{
    public class MyButton { }
}
",
@"<UserControl x:Class=""Q.W.Host""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:a=""clr-namespace:Old.Controls""
    xmlns:b=""clr-namespace:Old.Controls;assembly=MyApp"">
    <StackPanel>
        <a:MyButton />
        <b:MyButton />
    </StackPanel>
</UserControl>",
                "MyApp.Controls",
                @"Controls\MyButton.cs");

            Assert.DoesNotContain("Old.Controls", xaml);
            Assert.Equal(1, CountOf(xaml, @"clr-namespace:MyApp.Controls"""));
            Assert.Equal(1, CountOf(xaml, @"clr-namespace:MyApp.Controls;assembly=MyApp"""));
            Assert.DoesNotContain("<a:MyButton", xaml);
            Assert.DoesNotContain("<b:MyButton", xaml);
        }

        /// <summary>
        /// Only the exact mapping counts: a class of the same name in a child namespace, in a
        /// namespace which merely starts with the same words, and a class of the default
        /// (presentation) namespace stay as they are.
        /// </summary>
        [Fact]
        public async Task A_class_of_the_same_name_in_a_similar_namespace_is_not_touched()
        {
            var xaml = await MoveAsync(
@"namespace Old.Controls
{
    public class Button { }
}
",
@"<UserControl x:Class=""Q.W.Host""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:Old.Controls""
    xmlns:child=""clr-namespace:Old.Controls.Sub""
    xmlns:sibling=""clr-namespace:Old.ControlsX"">
    <StackPanel>
        <Button />
        <local:Button />
        <child:Button />
        <sibling:Button />
    </StackPanel>
</UserControl>",
                "MyApp.Controls",
                @"Controls\Button.cs");

            var alias = AliasOf(xaml, "MyApp.Controls");

            Assert.Contains("<Button />", xaml);
            Assert.Contains("<" + alias + ":Button />", xaml);
            Assert.Contains("<child:Button />", xaml);
            Assert.Contains("<sibling:Button />", xaml);
            Assert.Contains(@"clr-namespace:Old.Controls.Sub""", xaml);
            Assert.Contains(@"clr-namespace:Old.ControlsX""", xaml);
            Assert.DoesNotContain(@"clr-namespace:Old.Controls""", xaml);
        }

        /// <summary>
        /// The xaml is a plain text, so a markup extension may be broken over lines and the
        /// spaces inside of the braces are the user's own.
        /// </summary>
        [Fact]
        public async Task A_markup_extension_broken_over_lines_is_followed()
        {
            var xaml = await MoveAsync(ItemBody, Host("Old.Models",
@"    <StackPanel>
        <TextBlock Text=""{x:Static
                           local:Item.Default}"" />
        <TextBlock Tag=""{x:Type  local:Item }"" />
        <TextBlock Tag=""{ x:Type local:Item}"" />
    </StackPanel>"),
                "MyApp.Models");

            var alias = AliasOf(xaml, "MyApp.Models");

            Assert.DoesNotContain("local:", xaml);
            Assert.Equal(3, CountOf(xaml, alias + ":Item"));
        }

        /// <summary>
        /// A mapping of the class to an <c>XmlnsDefinition</c> uri (<c>xmlns:my="http://..."</c>)
        /// is no CLR namespace in the xaml, so the xaml has nothing to change.
        /// </summary>
        [Fact]
        public async Task A_uri_mapping_of_a_class_is_left_alone()
        {
            var body =
@"<UserControl x:Class=""Q.W.Host""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:my=""http://schemas.my.com/controls"">
    <my:MyButton />
</UserControl>";

            var xaml = await MoveAsync(
@"namespace Old.Controls
{
    public class MyButton { }
}
",
                body,
                "MyApp.Controls",
                @"Controls\MyButton.cs");

            Assert.Equal(body, xaml);
        }

        /// <summary>
        /// The <c>x:Class</c> names the namespace as well as the class: a class of the same
        /// name in another namespace is not the one which moved.
        /// </summary>
        [Fact]
        public void The_x_Class_of_a_class_of_the_same_name_in_another_namespace_is_not_touched()
        {
            var body =
@"<Window x:Class=""C.D.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
</Window>";

            Assert.Equal(body, MemoryXamlBodyProvider.MoveObject(body, "A.B", "MainWindow", "X.Y"));
        }

        #endregion

        #region XmlnsDefinition

        private const string XmlnsAttributesBody =
@"namespace System.Windows.Markup
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class XmlnsDefinitionAttribute : Attribute
    {
        public XmlnsDefinitionAttribute(string xmlNamespace, string clrNamespace) { }
    }
}
";

        /// <summary>
        /// Was red: <c>[assembly: XmlnsDefinition("http://schemas.my.com/controls", "Old.Controls")]</c>
        /// publishes the namespace under an uri, and the xaml which uses the uri
        /// (<c>&lt;my:MyButton/&gt;</c>) is not touched by the adjusting. The attribute holds the
        /// CLR namespace as a string and is no reference for Roslyn, so it keeps pointing to the
        /// namespace the class has left: everything compiles, and the xaml which uses the uri
        /// stops finding the class at run time.
        /// </summary>
        [Fact]
        public async Task The_XmlnsDefinition_attribute_follows_the_moved_namespace()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, "XmlnsAttributes.cs", XmlnsAttributesBody)
                .AddDocument(Project, @"Properties\AssemblyInfo.cs",
@"using System.Windows.Markup;

[assembly: XmlnsDefinition(""http://schemas.my.com/controls"", ""Old.Controls"")]
")
                .AddDocument(Project, @"Controls\MyButton.cs",
@"namespace Old.Controls
{
    public class MyButton { }
}
");

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, Project, @"Controls\MyButton.cs", "MyApp.Controls");

            var text = solution.TextOf(Project, @"Properties\AssemblyInfo.cs");

            Assert.Contains(@"""MyApp.Controls""", text);
            Assert.DoesNotContain(@"""Old.Controls""", text);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The old namespace keeps another type, so the uri has to map both namespaces: the
        /// attribute of the old one stays and a copy for the new one is added. Once the last
        /// type has left too, the mapping of the emptied namespace goes away.
        /// </summary>
        [Fact]
        public async Task The_XmlnsDefinition_attribute_of_a_namespace_which_stays_alive_gets_a_sibling()
        {
            const string OldMapping = @"[assembly: XmlnsDefinition(""http://schemas.my.com/controls"", ""Old.Controls"")]";
            const string NewMapping = @"[assembly: XmlnsDefinition(""http://schemas.my.com/controls"", ""MyApp.Controls"")]";

            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, "XmlnsAttributes.cs", XmlnsAttributesBody)
                .AddDocument(Project, @"Properties\AssemblyInfo.cs",
@"using System.Windows.Markup;
" + OldMapping + @"
")
                .AddDocument(Project, @"Controls\MyButton.cs",
@"namespace Old.Controls
{
    public class MyButton { }
}
")
                .AddDocument(Project, @"Controls\MyLabel.cs",
@"namespace Old.Controls
{
    public class MyLabel { }
}
");

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, Project, @"Controls\MyButton.cs", "MyApp.Controls");

            var text = solution.TextOf(Project, @"Properties\AssemblyInfo.cs");

            Assert.Contains(OldMapping, text);
            Assert.Contains(NewMapping, text);
            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, Project, @"Controls\MyLabel.cs", "MyApp.Controls");

            text = solution.TextOf(Project, @"Properties\AssemblyInfo.cs");

            Assert.DoesNotContain(OldMapping, text);
            Assert.Equal(1, CountOf(text, NewMapping));
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        #endregion

        #region The text level of the parser

        /// <summary>
        /// Was red: xml allows single quotes around an attribute value, and the regexes of
        /// <c>XamlDocument.ReadXmlns</c> know the double ones only. The mapping is not
        /// recognized, the tag keeps pointing to the namespace the class has left.
        /// </summary>
        [Fact]
        public async Task A_mapping_in_single_quotes_is_followed()
        {
            var xaml = await MoveAsync(
@"namespace Old.Controls
{
    public class MyButton { }
}
",
@"<UserControl x:Class=""Q.W.Host""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local='clr-namespace:Old.Controls'>
    <local:MyButton />
</UserControl>",
                "MyApp.Controls",
                @"Controls\MyButton.cs");

            Assert.DoesNotContain("Old.Controls", xaml);
            Assert.Contains("MyApp.Controls", xaml);
        }

        /// <summary>
        /// Was red: the same for the <c>x:Class</c> attribute (<c>XamlDocument.ReadClasses</c>).
        /// </summary>
        [Fact]
        public void An_x_Class_in_single_quotes_is_followed()
        {
            var body =
@"<Window x:Class='A.B.MainWindow'
    xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
</Window>";

            Assert.Contains("X.Y.MainWindow", MemoryXamlBodyProvider.MoveObject(body, "A.B", "MainWindow", "X.Y"));
        }

        /// <summary>
        /// Was red: a CLR namespace may be the default namespace of an element
        /// (<c>&lt;MyButton xmlns="clr-namespace:Old.Controls"/&gt;</c>). Such a declaration has
        /// no alias, <c>XamlDocument.ReadXmlns</c> looks for <c>xmlns:alias</c> only, and the
        /// class is left behind.
        /// </summary>
        [Fact]
        public async Task A_clr_namespace_as_the_default_namespace_of_an_element_is_followed()
        {
            var xaml = await MoveAsync(
@"namespace Old.Controls
{
    public class MyButton { }
}
",
@"<UserControl x:Class=""Q.W.Host""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <Grid>
        <MyButton xmlns=""clr-namespace:Old.Controls"" />
    </Grid>
</UserControl>",
                "MyApp.Controls",
                @"Controls\MyButton.cs");

            Assert.DoesNotContain("Old.Controls", xaml);
            Assert.Contains(@"xmlns=""clr-namespace:MyApp.Controls""", xaml);
        }

        /// <summary>
        /// Was red: the new mapping is written behind the last <c>xmlns</c> of the file
        /// (<c>XamlDocument.MoveObject</c>, <c>reloadedXmlns.Max</c>), whatever element that one
        /// is declared on. A declaration on a nested element makes the new alias visible inside
        /// of that element only, and the tags outside use an undeclared prefix.
        /// </summary>
        [Fact]
        public async Task The_new_mapping_is_declared_on_the_root_and_not_on_a_nested_element()
        {
            var xaml = await MoveAsync(
@"namespace Old.Controls
{
    public class MyButton { }
}
",
                Host("Old.Controls",
@"    <StackPanel>
        <local:MyButton />
        <Grid xmlns:other=""clr-namespace:Q.W"">
            <other:Foo />
        </Grid>
        <local:MyButton />
    </StackPanel>"),
                "MyApp.Controls",
                @"Controls\MyButton.cs");

            Assert.Null(Record.Exception(() => XDocument.Parse(xaml)));
        }

        /// <summary>
        /// Was red: the same prefix may be declared again on a nested element for another
        /// namespace. <c>XamlStructure.GetByAlias</c> knows the first declaration of the
        /// document only, so the nested <c>local:Foo</c> (<c>Old.B.Foo</c>) is taken for
        /// <c>Old.A.Foo</c> and stays behind.
        /// </summary>
        [Fact]
        public async Task The_prefix_declared_again_in_a_nested_scope_is_resolved_by_the_scope()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"A\Foo.cs",
@"namespace Old.A
{
    public class Foo { }
}
")
                .AddDocument(Project, @"B\Foo.cs",
@"namespace Old.B
{
    public class Foo { }
}
");

            solution.AddXamlFile(Project, "Host.xaml", Host("Old.A",
@"    <StackPanel>
        <local:Foo />
        <Grid xmlns:local=""clr-namespace:Old.B"">
            <local:Foo />
        </Grid>
    </StackPanel>"));

            await MoveAsync(solution, @"B\Foo.cs", "MyApp.B");

            var xaml = solution.XamlTextOf(Project, "Host.xaml");

            var namespaces = XDocument.Parse(xaml)
                .Descendants()
                .Where(e => e.Name.LocalName == "Foo")
                .Select(e => e.Name.NamespaceName)
                .ToList();

            Assert.Equal(new[] { "clr-namespace:Old.A", "clr-namespace:MyApp.B" }, namespaces);
        }

        /// <summary>
        /// Was red: <c>XamlDocument.Cleanup</c> removes every <c>xmlns</c> which has no user in the
        /// body, not only those which the move has left without one. The template of a
        /// window declares <c>xmlns:local</c> before anything uses it, and the first adjusted
        /// reference of the file silently deletes such a mapping together with the others
        /// the user may have prepared; the documented behaviour is to remove "the aliases
        /// which became unused".
        /// </summary>
        [Fact]
        public async Task A_mapping_which_was_unused_before_the_move_stays()
        {
            var xaml = await MoveAsync(
@"namespace Old.Controls
{
    public class MyButton { }
}
",
@"<UserControl x:Class=""Q.W.Host""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:Old.Controls""
    xmlns:prepared=""clr-namespace:Q.Prepared"">
    <local:MyButton />
</UserControl>",
                "MyApp.Controls",
                @"Controls\MyButton.cs");

            Assert.Contains(@"xmlns:prepared=""clr-namespace:Q.Prepared""", xaml);
        }

        #endregion

        #region Whole windows and the generated code

        /// <summary>
        /// A user control with its code behind is moved the way the wizard does it: both files
        /// are chosen. Its <c>x:Class</c>, the reference to itself inside of its own xaml and
        /// the tag of it in another window follow the class.
        /// </summary>
        [Fact]
        public async Task A_user_control_with_its_code_behind_is_moved_by_a_session()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Views\MyControl.xaml.cs",
@"namespace Old.Views
{
    public partial class MyControl { }
}
")
                .AddDocument(Project, "Main.xaml.cs",
@"namespace MyApp
{
    public partial class Main { }
}
");

            solution.AddXamlFile(Project, @"Views\MyControl.xaml",
@"<UserControl x:Class=""Old.Views.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:Old.Views"">
    <UserControl.Resources>
        <Style TargetType=""{x:Type local:MyControl}"" />
    </UserControl.Resources>
</UserControl>");
            solution.AddXamlFile(Project, "Main.xaml",
@"<Window x:Class=""MyApp.Main""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:views=""clr-namespace:Old.Views"">
    <views:MyControl />
</Window>");

            Assert.Empty(await solution.CompilationErrorsAsync());

            var outcome = await RunSessionAsync(
                solution,
                solution.PathOf(Project, @"Views\MyControl.xaml.cs"),
                solution.PathOf(Project, @"Views\MyControl.xaml")
                );

            Assert.Equal(AdjustSessionOutcome.Completed, outcome);
            Assert.Contains("namespace MyApp.Views", solution.TextOf(Project, @"Views\MyControl.xaml.cs"));
            Assert.Empty(await solution.CompilationErrorsAsync());

            var control = solution.XamlTextOf(Project, @"Views\MyControl.xaml");
            var controlAlias = AliasOf(control, "MyApp.Views");

            Assert.Contains(@"x:Class=""MyApp.Views.MyControl""", control);
            Assert.Contains("{x:Type " + controlAlias + ":MyControl}", control);
            Assert.DoesNotContain("Old.Views", control);
            AssertWellFormed(control);

            var main = solution.XamlTextOf(Project, "Main.xaml");

            Assert.Contains("<" + AliasOf(main, "MyApp.Views") + ":MyControl />", main);
            Assert.DoesNotContain("Old.Views", main);
            AssertWellFormed(main);
        }

        /// <summary>
        /// Was red: <c>x:Class</c> of a xaml and the partial class of its code behind may be in
        /// different folders. The planner gives every file the namespace of its own folder, so
        /// the two halves of the class land in two different namespaces
        /// (<c>MyApp.Views.Shell</c> in the xaml, <c>MyApp.Code.Shell</c> in the C#): the project
        /// does not build after that. The planner asks for the code behind only to learn
        /// whether it is compiled by several projects.
        /// </summary>
        [Fact]
        public async Task A_xaml_and_its_code_behind_from_different_folders_stay_in_one_namespace()
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Code\Shell.cs",
@"namespace Old
{
    public partial class Shell { }
}
");

            solution.AddXamlFile(Project, @"Views\Shell.xaml",
@"<Window x:Class=""Old.Shell""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>");

            await RunSessionAsync(
                solution,
                solution.PathOf(Project, @"Views\Shell.xaml"),
                solution.PathOf(Project, @"Code\Shell.cs")
                );

            var xamlClass = Regex.Match(solution.XamlTextOf(Project, @"Views\Shell.xaml"), @"x:Class=""([^""]+)""").Groups[1].Value;
            var csNamespace = Regex.Match(solution.TextOf(Project, @"Code\Shell.cs"), @"namespace\s+([\w.]+)").Groups[1].Value;

            Assert.Equal(csNamespace + ".Shell", xamlClass);
        }

        /// <summary>
        /// The named elements of a window are fields of the generated half of its class
        /// (<c>obj\...\MainWindow.g.cs</c>) which spell the type of the element out. The type
        /// moves, the xaml follows it, and the next build writes the generated half again with
        /// the new namespace: the old namespace must not be imported into the code behind
        /// meanwhile.
        /// </summary>
        [Fact]
        public async Task A_named_element_of_the_moved_class_survives_the_regeneration_of_the_code_behind()
        {
            const string GeneratedFilePath = @"obj\Debug\MainWindow.g.cs";

            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, @"Controls\MyButton.cs",
@"namespace Old.Controls
{
    public class MyButton { }
}
")
                .AddDocument(Project, "MainWindow.xaml.cs",
@"namespace MyApp
{
    public partial class MainWindow
    {
        public void Click() => btn.GetType();
    }
}
")
                .AddDocument(Project, GeneratedFilePath,
@"namespace MyApp
{
    public partial class MainWindow
    {
        internal global::Old.Controls.MyButton btn;

        public void InitializeComponent() { }
    }
}
");

            var xamlFilePath = solution.AddXamlFile(Project, "MainWindow.xaml",
@"<Window x:Class=""MyApp.MainWindow""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:Old.Controls"">
    <local:MyButton x:Name=""btn"" />
</Window>");

            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(solution, Project, @"Controls\MyButton.cs", "MyApp.Controls", xamlFilePath);

            var xaml = solution.XamlTextOf(Project, "MainWindow.xaml");

            Assert.Contains("<" + AliasOf(xaml, "MyApp.Controls") + @":MyButton x:Name=""btn"" />", xaml);
            AssertWellFormed(xaml);

            //the build after the adjusting regenerates the code behind out of the new xaml
            solution.ReplaceDocument(Project, GeneratedFilePath,
@"namespace MyApp
{
    public partial class MainWindow
    {
        internal global::MyApp.Controls.MyButton btn;

        public void InitializeComponent() { }
    }
}
");

            Assert.Empty(await solution.CompilationErrorsAsync());
            Assert.DoesNotContain("Old.Controls", solution.TextOf(Project, "MainWindow.xaml.cs"));
        }

        #endregion

        /// <summary>
        /// Move the C# file of the fixture solution (one project, one C# file, one xaml file)
        /// and read the xaml back.
        /// </summary>
        /// <param name="csBody">Content of the C# file.</param>
        /// <param name="xaml">Content of the xaml file.</param>
        /// <param name="targetNamespace">The namespace the types of the C# file are moved into.</param>
        /// <param name="csFile">Path of the C# file inside of the project.</param>
        /// <param name="xamlFile">Path of the xaml file inside of the project.</param>
        private static async System.Threading.Tasks.Task<string> MoveAsync(
            string csBody,
            string xaml,
            string targetNamespace,
            string csFile = @"Models\Item.cs",
            string xamlFile = "Host.xaml"
            )
        {
            using var solution = new TestSolution()
                .AddProject(Project)
                .AddDocument(Project, csFile, csBody)
                ;

            solution.AddXamlFile(Project, xamlFile, xaml);

            await MoveAsync(solution, csFile, targetNamespace, xamlFile);

            var result = solution.XamlTextOf(Project, xamlFile);

            AssertWellFormed(result);

            return result;
        }

        /// <summary>
        /// Move a C# file of the solution, the xaml files of it are the candidates to follow.
        /// The solution compiles before and after.
        /// </summary>
        private static async System.Threading.Tasks.Task MoveAsync(
            TestSolution solution,
            string csFile,
            string targetNamespace,
            string xamlFile = "Host.xaml"
            )
        {
            Assert.Empty(await solution.CompilationErrorsAsync());

            await AdjustAndCleanupAsync(
                solution,
                Project,
                csFile,
                targetNamespace,
                solution.PathOf(Project, xamlFile)
                );

            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// Run the adjusting session over the files the way the last step of the wizard does.
        /// </summary>
        private static async System.Threading.Tasks.Task<AdjustSessionOutcome> RunSessionAsync(
            TestSolution solution,
            params string[] subjectFilePaths
            )
        {
            return await new AdjustSession(
                    solution.Context,
                    new NamespaceReplaceRegex(string.Empty, string.Empty)
                    )
                .RunAsync(subjectFilePaths);
        }

        /// <summary>
        /// A user control which maps the given CLR namespace to <c>local</c> and has the given
        /// markup as its content.
        /// </summary>
        private static string Host(string clrNamespace, string body)
        {
            return
@"<UserControl x:Class=""Q.W.Host""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:d=""http://schemas.microsoft.com/expression/blend/2008""
    xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006""
    xmlns:local=""clr-namespace:" + clrNamespace + @"""
    mc:Ignorable=""d"">
" + body + @"
</UserControl>";
        }

        /// <summary>
        /// The alias the xaml maps the given CLR namespace to. The alias of a new mapping is
        /// generated, so a test asks for it.
        /// </summary>
        private static string AliasOf(string xaml, string clrNamespace)
        {
            var match = Regex.Match(
                xaml,
                @"xmlns:(\w+)\s*=\s*""clr-namespace:" + Regex.Escape(clrNamespace) + @"(;[^""]*)?"""
                );

            Assert.True(match.Success, $"There is no mapping of {clrNamespace} in:\n{xaml}");

            return match.Groups[1].Value;
        }

        /// <summary>
        /// The xaml is well formed xml and every prefix it uses is declared.
        /// </summary>
        private static void AssertWellFormed(string xaml)
        {
            XDocument.Parse(xaml);
        }
    }
}
