using AdjustNamespace.Tests.Infrastructure;
using Xunit;

namespace AdjustNamespace.Tests.Xaml
{
    /// <summary>
    /// Tests of <see cref="AdjustNamespace.Xaml.XamlDocument"/>: the whole xaml subsystem
    /// works with a plain string, so no file and no Visual Studio is required here.
    /// </summary>
    public class XamlDocumentTests
    {
        [Fact]
        public void A_control_of_the_moved_class_gets_the_new_namespace()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y", result);
            Assert.Contains(":MyButton", result);
        }

        [Fact]
        public void A_closing_tag_of_the_moved_class_gets_the_new_namespace_too()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton>
    </local:MyButton>
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.DoesNotContain("<local:MyButton", result);
            Assert.DoesNotContain("</local:MyButton", result);
            Assert.Contains("<y:MyButton>", result);
            Assert.Contains("</y:MyButton>", result);
        }

        [Fact]
        public void The_class_of_the_document_itself_is_moved()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyControl", "X.Y");

            Assert.Contains(@"x:Class=""X.Y.MyControl""", result);
        }

        [Fact]
        public void A_reference_inside_the_x_Type_markup_extension_is_moved()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <Style TargetType=""{x:Type local:MyButton}"" />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y", result);
            Assert.DoesNotContain("local:MyButton", result);
        }

        [Fact]
        public void A_reference_inside_the_x_Static_markup_extension_is_moved()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <TextBlock Text=""{x:Static local:Consts.Title}"" />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "Consts", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y", result);
            Assert.DoesNotContain("local:Consts", result);
        }

        [Fact]
        public void The_assembly_suffix_of_the_source_xmlns_is_inherited_by_the_new_one()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B;assembly=MyAssembly"">
    <local:MyButton />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y;assembly=MyAssembly", result);
        }

        [Fact]
        public void An_existing_xmlns_of_the_target_namespace_is_reused()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    xmlns:target=""clr-namespace:X.Y"">
    <local:MyButton />
    <target:AnotherControl />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains("<target:MyButton", result);
            //no second declaration of the same namespace
            Assert.Equal(1, CountOf(result, "clr-namespace:X.Y"));
        }

        [Fact]
        public void A_class_of_another_namespace_is_not_touched()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:other=""clr-namespace:Q.W"">
    <other:MyButton />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Equal(body, result);
        }

        [Fact]
        public void The_maui_xaml_language_uri_is_recognized()
        {
            var body =
@"<ContentPage x:Class=""A.B.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml"">
</ContentPage>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MainPage", "X.Y");

            Assert.Contains(@"x:Class=""X.Y.MainPage""", result);
        }

        [Fact]
        public void A_renamed_alias_of_the_xaml_language_namespace_is_recognized()
        {
            var body =
@"<UserControl xaml:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:xaml=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyControl", "X.Y");

            Assert.Contains(@"xaml:Class=""X.Y.MyControl""", result);
        }

        [Fact]
        public void Nothing_is_changed_when_there_is_nothing_to_change()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <TextBlock Text=""hello"" />
</UserControl>";

            var (document, _) = MemoryXamlBodyProvider.CreateDocument(body);

            var modified = document.MoveObject("Q.W", "Unknown", "X.Y");

            Assert.False(modified.IsChangesExists(document));
        }

        /// <summary>
        /// An alias may be used by a custom markup extension, which is neither a tag nor
        /// an <c>x:Type</c>/<c>x:Static</c> reference: such an xmlns clause has to survive
        /// the cleanup, otherwise the document does not compile anymore.
        /// </summary>
        [Fact]
        public void The_xmlns_used_by_a_custom_markup_extension_is_kept()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    xmlns:conv=""clr-namespace:A.Converters"">
    <local:MyButton Content=""{conv:UpperCase Text=abc}"" />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains("clr-namespace:A.Converters", result);
        }

        /// <summary>
        /// The same as above, but with an attached property (<c>attached:MyGrid.Row="0"</c>).
        /// </summary>
        [Fact]
        public void The_xmlns_used_by_an_attached_property_is_kept()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    xmlns:attached=""clr-namespace:A.Attached"">
    <local:MyButton attached:Helper.IsEnabled=""True"" />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains("clr-namespace:A.Attached", result);
        }

        /// <summary>
        /// <c>MoveObject</c> writes the newly created xmlns clauses after the last existing one
        /// and does nothing at all if there is no clr-namespace clause in the document
        /// (<c>reloadedXmlns.Count > 0</c>). The x:Class attribute needs no xmlns, so such
        /// a document is processed correctly; the test guards that branch.
        /// </summary>
        [Fact]
        public void A_document_without_any_clr_namespace_clause_is_processed()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
    <TextBlock Text=""hello"" />
</UserControl>";

            var (document, provider) = MemoryXamlBodyProvider.CreateDocument(body);

            //the class of the document is moved: it must not throw and must not lose the content
            var modified = document.MoveObject("A.B", "MyControl", "X.Y");
            modified.SaveIfChangesExistsAgainst(document);

            Assert.Contains(@"x:Class=""X.Y.MyControl""", provider.Body);
            Assert.Contains(@"<TextBlock Text=""hello"" />", provider.Body);
        }

        /// <summary>
        /// A tag whose alias is not a clr-namespace one (<c>&lt;x:Array&gt;</c>) is simply
        /// not a subject to move, see <see cref="AdjustNamespace.Xaml.XamlStructure.GetByAlias"/>.
        /// </summary>
        [Fact]
        public void A_tag_with_a_non_clr_alias_does_not_break_the_processing()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <UserControl.Resources>
        <x:Array x:Key=""items"" Type=""sys:String"" />
    </UserControl.Resources>
    <local:Array />
</UserControl>";

            //a class named `Array` is moved; the <x:Array> tag has the same class name
            //but its alias `x` is not a clr-namespace one
            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "Array", "X.Y");

            Assert.Contains("clr-namespace:X.Y", result);
        }

        /// <summary>
        /// The xaml is parsed with the regexes over the plain text, so the commented out
        /// markup has to be excluded from the parsing explicitly.
        /// </summary>
        [Fact]
        public void A_commented_out_tag_is_not_touched()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <!-- <local:MyButton /> -->
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Equal(body, result);
        }

        /// <summary>
        /// A ResourceDictionary has no <c>x:Class</c> and may have no xaml language namespace
        /// at all, but its tags still reference the moved classes.
        /// </summary>
        [Fact]
        public void A_document_without_the_xaml_language_namespace_is_processed()
        {
            var body =
@"<ResourceDictionary
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyConverter />
</ResourceDictionary>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyConverter", "X.Y");

            Assert.Contains("clr-namespace:X.Y", result);
            Assert.DoesNotContain("<local:MyConverter", result);
        }

        /// <summary>
        /// <c>&lt;local:MyControl.Resources&gt;</c> is a property element, not a class
        /// reference of its own: the tag has to be renamed together with the class
        /// and its property part has to survive.
        /// </summary>
        [Fact]
        public void A_property_element_of_the_moved_class_is_processed()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton>
        <local:MyButton.Content>
            <TextBlock Text=""hello"" />
        </local:MyButton.Content>
    </local:MyButton>
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.DoesNotContain("local:MyButton", result);
            Assert.Contains(".Content>", result);
            Assert.Equal(2, CountOf(result, ":MyButton.Content"));
        }

        /// <summary>
        /// The classes of a namespace are moved one by one, and every one of them is a separate
        /// <c>MoveObject</c> call over the previous result: the second class has to reuse
        /// the xmlns clause created for the first one.
        /// </summary>
        [Fact]
        public void The_second_moved_class_reuses_the_new_xmlns()
        {
            var body =
@"<UserControl x:Class=""Q.W.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton />
    <local:MyLabel />
</UserControl>";

            var (document, provider) = MemoryXamlBodyProvider.CreateDocument(body);

            var modified = document
                .MoveObject("A.B", "MyButton", "X.Y")
                .MoveObject("A.B", "MyLabel", "X.Y")
                ;
            modified.SaveIfChangesExistsAgainst(document);

            Assert.Equal(1, CountOf(provider.Body, "clr-namespace:X.Y"));
            Assert.DoesNotContain("local:", provider.Body);
        }

        /// <summary>
        /// Only one class of the namespace is moved, so its alias is still used
        /// by the class which stays and must not be removed.
        /// </summary>
        [Fact]
        public void The_xmlns_of_a_partially_moved_namespace_is_kept()
        {
            var body =
@"<UserControl x:Class=""Q.W.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton />
    <local:MyLabel />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains("clr-namespace:A.B", result);
            Assert.Contains("<local:MyLabel", result);
            Assert.DoesNotContain("<local:MyButton", result);
        }

        /// <summary>
        /// A class of another namespace with the very same name is not a subject to move,
        /// even when the namespace being moved is declared in the same document.
        /// </summary>
        [Fact]
        public void A_class_with_the_same_name_in_another_namespace_is_not_moved()
        {
            var body =
@"<UserControl x:Class=""Q.W.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    xmlns:other=""clr-namespace:C.D"">
    <local:MyButton />
    <other:MyButton />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains("<other:MyButton", result);
            Assert.Contains("clr-namespace:C.D", result);
            Assert.DoesNotContain("<local:MyButton", result);
        }

        /// <summary>
        /// The commented out markup is excluded from the parsing, but the xmlns clause it uses
        /// must not be removed: the user is going to uncomment that markup one day.
        /// </summary>
        [Fact]
        public void The_xmlns_used_by_a_commented_out_tag_only_is_kept()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    xmlns:disabled=""clr-namespace:A.Disabled"">
    <local:MyButton />
    <!-- <disabled:OldButton /> -->
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains("clr-namespace:A.Disabled", result);
        }

        /// <summary>
        /// The same namespace may be declared twice with two different aliases;
        /// both of them reference the moved class and both have to be processed.
        /// </summary>
        [Fact]
        public void A_namespace_declared_with_two_aliases_is_processed()
        {
            var body =
@"<UserControl x:Class=""Q.W.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    xmlns:local2=""clr-namespace:A.B"">
    <local:MyButton />
    <local2:MyButton />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.DoesNotContain("<local:MyButton", result);
            Assert.DoesNotContain("<local2:MyButton", result);
            Assert.Contains("clr-namespace:X.Y", result);
        }

        /// <summary>
        /// An existing declaration of the target namespace is reused only if it points to
        /// the same place: <c>clr-namespace:X.Y;assembly=External</c> is another assembly,
        /// and a class of this one must not be redirected there.
        /// </summary>
        [Fact]
        public void An_xmlns_of_the_target_namespace_in_another_assembly_is_not_reused()
        {
            var body =
@"<UserControl x:Class=""Q.W.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    xmlns:ext=""clr-namespace:X.Y;assembly=External"">
    <local:MyButton />
    <ext:ExternalControl />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.DoesNotContain("ext:MyButton", result);
            Assert.Contains(@"clr-namespace:X.Y""", result);
        }

        [Fact]
        public void The_root_class_of_the_document_is_reported()
        {
            var (document, _) = MemoryXamlBodyProvider.CreateDocument(
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>");

            Assert.True(document.GetRootInfo(out var rootNamespace, out var rootName));
            Assert.Equal("A.B", rootNamespace);
            Assert.Equal("MyControl", rootName);
        }

        /// <summary>
        /// A code behind class may live in the global namespace; the namespace part
        /// of <c>x:Class</c> is empty then.
        /// </summary>
        [Fact]
        public void The_root_class_without_a_namespace_is_reported()
        {
            var (document, _) = MemoryXamlBodyProvider.CreateDocument(
@"<UserControl x:Class=""MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>");

            Assert.True(document.GetRootInfo(out var rootNamespace, out var rootName));
            Assert.Equal("", rootNamespace);
            Assert.Equal("MyControl", rootName);
        }

        [Fact]
        public void A_document_without_the_root_class_reports_nothing()
        {
            var (document, _) = MemoryXamlBodyProvider.CreateDocument(
@"<ResourceDictionary
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
</ResourceDictionary>");

            Assert.False(document.GetRootInfo(out var rootNamespace, out var rootName));
            Assert.Null(rootNamespace);
            Assert.Null(rootName);
        }

        [Fact]
        public void The_spaces_around_the_x_Class_assignment_do_not_matter()
        {
            var body =
@"<UserControl x:Class = ""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</UserControl>";

            var (document, _) = MemoryXamlBodyProvider.CreateDocument(body);

            Assert.True(document.GetRootInfo(out var rootNamespace, out _));
            Assert.Equal("A.B", rootNamespace);

            Assert.Contains(@"x:Class=""X.Y.MyControl""", MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyControl", "X.Y"));
        }

        /// <summary>
        /// MAUI pages use the 2009 xaml language uri and their own default xmlns; the root
        /// class must still move, and a <c>clr-namespace</c> mapping of a custom control too.
        /// </summary>
        [Fact]
        public void A_maui_page_moves_its_root_class_and_a_clr_namespace_control()
        {
            var body =
@"<ContentPage x:Class=""A.B.MainPage""
    xmlns=""http://schemas.microsoft.com/dotnet/2021/maui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2009/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton />
</ContentPage>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y", result);
            Assert.DoesNotContain("<local:MyButton", result);

            result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MainPage", "X.Y");
            Assert.Contains(@"x:Class=""X.Y.MainPage""", result);
        }

        /// <summary>
        /// Avalonia prefers <c>xmlns:local="using:…"</c>. The mapping has to be rewritten
        /// in the same form, not as a WPF <c>clr-namespace</c>.
        /// </summary>
        [Fact]
        public void An_avalonia_using_xmlns_is_rewritten_as_using()
        {
            var body =
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""using:A.B"">
    <local:MyButton />
</Window>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains(@"using:X.Y", result);
            Assert.DoesNotContain("clr-namespace:", result);
            Assert.DoesNotContain("<local:MyButton", result);
        }

        /// <summary>
        /// Avalonia's root class uses the same <c>x:Class</c> attribute as WPF.
        /// </summary>
        [Fact]
        public void An_avalonia_window_root_class_is_moved()
        {
            var body =
@"<Window x:Class=""A.B.MainWindow""
    xmlns=""https://github.com/avaloniaui""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
</Window>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MainWindow", "X.Y");

            Assert.Contains(@"x:Class=""X.Y.MainWindow""", result);
        }

        /// <summary>
        /// A new declaration gets the last part of its namespace as the alias and is written
        /// like its neighbours: on a line of its own with their indentation. The declaration
        /// which became unused goes away with its line.
        /// </summary>
        [Fact]
        public void A_new_declaration_is_named_after_its_namespace_and_written_like_the_others()
        {
            var body =
@"<UserControl x:Class=""A.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    Background=""White"">
    <local:MyButton />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "A.Views");

            Assert.Equal(
@"<UserControl x:Class=""A.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:views=""clr-namespace:A.Views""
    Background=""White"">
    <views:MyButton />
</UserControl>",
                result);
        }

        [Fact]
        public void A_new_declaration_is_written_on_the_same_line_when_the_others_are()
        {
            var body =
@"<UserControl x:Class=""A.MyControl"" xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:A.B"">
    <local:MyButton />
    <local:Other />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "A.Views");

            Assert.Equal(
@"<UserControl x:Class=""A.MyControl"" xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" xmlns:local=""clr-namespace:A.B"" xmlns:views=""clr-namespace:A.Views"">
    <views:MyButton />
    <local:Other />
</UserControl>",
                result);
        }

        /// <summary>
        /// The alias of the new declaration differs from every alias of the document, a nested
        /// one included: the new declaration is written on the root element, and an alias of a
        /// nested element would shadow it inside of that element.
        /// </summary>
        [Fact]
        public void A_new_declaration_gets_a_number_when_its_alias_is_taken()
        {
            var body =
@"<UserControl x:Class=""A.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <StackPanel xmlns:views=""clr-namespace:Other.Views"">
        <local:MyButton />
        <views:Unrelated />
    </StackPanel>
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "A.Views");

            Assert.Contains(@"xmlns:views2=""clr-namespace:A.Views""", result);
            Assert.Contains("<views2:MyButton />", result);
            Assert.Contains("<views:Unrelated />", result);
        }

        /// <summary>
        /// The prefixes starting with <c>xml</c> are reserved by XML.
        /// </summary>
        [Fact]
        public void A_new_declaration_does_not_get_a_reserved_alias()
        {
            var body =
@"<UserControl x:Class=""A.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "A.Xml");

            Assert.Contains(@"xmlns:nsxml=""clr-namespace:A.Xml""", result);
            Assert.Contains("<nsxml:MyButton />", result);
        }

        private static int CountOf(string text, string substring)
        {
            var result = 0;
            var index = text.IndexOf(substring, StringComparison.Ordinal);
            while (index >= 0)
            {
                result++;
                index = text.IndexOf(substring, index + substring.Length, StringComparison.Ordinal);
            }

            return result;
        }
    }
}
