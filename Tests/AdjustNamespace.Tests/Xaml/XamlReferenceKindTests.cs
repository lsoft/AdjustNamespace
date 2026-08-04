using AdjustNamespace.Tests.Infrastructure;
using Xunit;

namespace AdjustNamespace.Tests.Xaml
{
    /// <summary>
    /// A xaml document references a class not only by a tag and by a
    /// <c>{x:Type}</c>/<c>{x:Static}</c> markup extension: the very same
    /// <c>alias:ClassName</c> pair is also written in the attribute values
    /// (<c>TargetType="local:MyButton"</c>), in the attached properties and in the custom
    /// markup extensions. Every such reference has to follow the moved class, otherwise
    /// the document points to a namespace which does not exist anymore.
    /// </summary>
    public class XamlReferenceKindTests
    {
        /// <summary>
        /// <c>TargetType</c> and <c>DataType</c> accept a bare <c>alias:ClassName</c>
        /// without any markup extension around it.
        /// </summary>
        [Fact]
        public void A_class_referenced_by_a_bare_attribute_value_is_moved()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <Style TargetType=""local:MyButton"" />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y", result);
        }

        /// <summary>
        /// An attached property is written as <c>alias:ClassName.PropertyName</c> inside
        /// the tag of another control.
        /// </summary>
        [Fact]
        public void A_class_of_an_attached_property_is_moved()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:attached=""clr-namespace:A.B"">
    <Button attached:Helper.IsEnabled=""True"" />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "Helper", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y", result);
        }

        /// <summary>
        /// A custom markup extension is written as <c>{alias:ClassName}</c>, and the class
        /// of it may be named both with and without the <c>Extension</c> suffix.
        /// </summary>
        [Fact]
        public void A_class_of_a_custom_markup_extension_is_moved()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:conv=""clr-namespace:A.B"">
    <TextBlock Text=""{conv:UpperCase}"" />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "UpperCase", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y", result);
        }

        /// <summary>
        /// The type arguments of a generic control are written in the
        /// <c>x:TypeArguments</c> attribute.
        /// </summary>
        [Fact]
        public void A_class_referenced_by_x_TypeArguments_is_moved()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    xmlns:collections=""clr-namespace:System.Collections.Generic;assembly=mscorlib"">
    <collections:List x:TypeArguments=""local:Item"" />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "Item", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y", result);
        }

        /// <summary>
        /// A class whose name merely starts with the name of the moved one is not a
        /// reference to it.
        /// </summary>
        [Fact]
        public void A_class_with_a_name_starting_with_the_moved_one_is_not_touched()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:ButtonEx />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "Button", "X.Y");

            Assert.Contains("<local:ButtonEx", result);
            Assert.DoesNotContain("clr-namespace:X.Y", result);
        }

        /// <summary>
        /// The whitespace around the <c>=</c> of an attribute is allowed by xml.
        /// </summary>
        [Fact]
        public void An_xmlns_written_with_the_spaces_around_the_assignment_is_processed()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local = ""clr-namespace:A.B"">
    <local:MyButton />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains(@"clr-namespace:X.Y", result);
        }

        /// <summary>
        /// A class moved into the namespace it is in already changes nothing.
        /// </summary>
        [Fact]
        public void A_class_moved_into_its_own_namespace_changes_nothing()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "A.B");

            Assert.Equal(body, result);
        }

        /// <summary>
        /// All the kinds of the references which are supported are fixed at once,
        /// and a single new xmlns clause is enough for all of them.
        /// </summary>
        [Fact]
        public void Every_supported_reference_of_one_class_is_moved_at_once()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B"">
    <local:MyButton Style=""{x:Static local:MyButton.DefaultStyle}"" />
    <ContentControl Content=""{x:Type local:MyButton}"" />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.DoesNotContain("clr-namespace:A.B", result);
            Assert.Equal(1, CountOf(result, "clr-namespace:X.Y"));
            Assert.DoesNotContain("local:", result);
        }

        /// <summary>
        /// The alias of the new xmlns clause is built from the last part of the target
        /// namespace, and such an alias may be taken by another namespace already.
        /// </summary>
        [Fact]
        public void The_generated_alias_does_not_collide_with_an_existing_one()
        {
            var body =
@"<UserControl x:Class=""A.B.MyControl""
    xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
    xmlns:local=""clr-namespace:A.B""
    xmlns:Y=""clr-namespace:Foreign"">
    <local:MyButton />
    <Y:ForeignControl />
</UserControl>";

            var result = MemoryXamlBodyProvider.MoveObject(body, "A.B", "MyButton", "X.Y");

            Assert.Contains(@"xmlns:Y=""clr-namespace:Foreign""", result);
            Assert.Contains("<Y:ForeignControl", result);
            Assert.Contains("clr-namespace:X.Y", result);
        }

        private static int CountOf(string text, string substring)
        {
            return AdjustRunner.CountOf(text, substring);
        }
    }
}
