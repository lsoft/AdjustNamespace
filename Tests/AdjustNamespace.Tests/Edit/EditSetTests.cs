using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Namespace;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AdjustNamespace.Tests.Edit
{
    /// <summary>
    /// Tests of <see cref="EditSet"/> — the description of everything an adjusting is going
    /// to change. It touches no solution, hence no <c>TestSolution</c> here.
    /// </summary>
    public class EditSetTests
    {
        private const string File1 = @"c:\solution\MyApp\Class1.cs";
        private const string File2 = @"c:\solution\MyApp\Class2.cs";

        [Fact]
        public void An_empty_set_changes_nothing()
        {
            var edits = new EditSet();

            Assert.True(edits.IsEmpty);
            Assert.Empty(edits.FilePaths);
            Assert.Empty(edits.EditsOf(File1));
        }

        [Fact]
        public void The_edits_are_grouped_by_file()
        {
            var edits = new EditSet();

            edits.AddUsing(File1, "X.Y");
            edits.AddUsing(File2, "X.Y");

            Assert.Equal(new[] { File1, File2 }, edits.FilePaths);
            Assert.Single(edits.EditsOf(File1));
            Assert.Single(edits.EditsOf(File2));
        }

        /// <summary>
        /// A file may reference the same moved type many times, and a single <c>using</c>
        /// clause covers all of these references.
        /// </summary>
        [Fact]
        public void The_same_edit_is_not_scheduled_twice()
        {
            var edits = new EditSet();

            edits.AddUsing(File1, "X.Y");
            edits.AddUsing(File1, "X.Y");
            edits.ReplaceText(File1, new TextSpan(10, 5), "X.Y.Class1");
            edits.ReplaceText(File1, new TextSpan(10, 5), "X.Y.Class1");
            edits.MoveNamespace(File1, new NamespaceTransition("A.B", "X.Y", true));
            edits.MoveNamespace(File1, new NamespaceTransition("A.B", "X.Y", true));

            Assert.Equal(3, edits.EditsOf(File1).Count);
        }

        /// <summary>
        /// Two references of one file to two different namespaces are two different edits,
        /// and so are two replacements of one span with a different text.
        /// </summary>
        [Fact]
        public void The_different_edits_are_all_kept()
        {
            var edits = new EditSet();

            edits.AddUsing(File1, "X.Y");
            edits.AddUsing(File1, "Q.W");
            edits.ReplaceText(File1, new TextSpan(10, 5), "X.Y.Class1");
            edits.ReplaceText(File1, new TextSpan(10, 5), "Q.W.Class1");

            Assert.Equal(4, edits.EditsOf(File1).Count);
        }

        [Fact]
        public void The_edits_of_a_file_keep_the_order_they_have_been_added_in()
        {
            var edits = new EditSet();

            edits.MoveNamespace(File1, new NamespaceTransition("A.B", "X.Y", true));
            edits.AddUsing(File1, "X.Y");
            edits.ReplaceText(File1, new TextSpan(10, 5), "X.Y.Class1");

            var fileEdits = edits.EditsOf(File1);

            Assert.IsType<MoveNamespaceEdit>(fileEdits[0]);
            Assert.IsType<AddUsingEdit>(fileEdits[1]);
            Assert.IsType<ReplaceTextEdit>(fileEdits[2]);
        }

        [Fact]
        public void An_edit_tells_what_it_is_about()
        {
            var edits = new EditSet();

            edits.AddUsing(File1, "X.Y");

            var edit = Assert.IsType<AddUsingEdit>(Assert.Single(edits.EditsOf(File1)));

            Assert.Equal(File1, edit.FilePath);
            Assert.Equal("X.Y", edit.NamespaceName);
        }
    }
}
