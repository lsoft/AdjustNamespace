using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Adjusting.Edit.Apply;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.Xaml.BodyProvider;
using Microsoft.CodeAnalysis.Text;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace AdjustNamespace.Tests.Edit
{
    /// <summary>
    /// Tests of the edit of a file that is no C# document (<see cref="ReplaceFileTextEdit"/>):
    /// the text is changed where the spans say, and the encoding, the byte order mark and the
    /// line breaks of the file stay as they are, the rule the xaml files follow as well.
    /// </summary>
    public class ReplaceFileTextTests
    {
        private const string Name = "Legacy.Models.Customer";

        private static TextSpan SpanOf(string text, string fragment, int occurrence = 0)
        {
            var index = -1;
            for (var i = 0; i <= occurrence; i++)
            {
                index = text.IndexOf(fragment, index + 1, System.StringComparison.Ordinal);
            }

            return new TextSpan(index, fragment.Length);
        }

        [Fact]
        public void The_replacements_are_made_from_the_end_with_the_spans_of_the_original_text()
        {
            var text = "a Legacy.Models.Customer b Legacy.Models.Customer c";

            var changed = ReplaceFileTextApplier.Apply(
                text,
                new[]
                {
                    new ReplaceFileTextEdit("f", SpanOf(text, Name, 1), "X.Y.Customer"),
                    new ReplaceFileTextEdit("f", SpanOf(text, Name, 0), "Lib.Models.Customer")
                }
                );

            Assert.Equal("a Lib.Models.Customer b X.Y.Customer c", changed);
        }

        [Fact]
        public void An_intersecting_replacement_is_dropped_and_the_longer_one_wins()
        {
            var text = "Legacy.Models.Customer.Name";

            var changed = ReplaceFileTextApplier.Apply(
                text,
                new[]
                {
                    new ReplaceFileTextEdit("f", new TextSpan(0, Name.Length), "Lib.Models.Customer"),
                    new ReplaceFileTextEdit("f", new TextSpan(7, 6), "Zzz"),
                    new ReplaceFileTextEdit("f", new TextSpan(0, text.Length), "Whole")
                }
                );

            Assert.Equal("Whole", changed);
        }

        [Fact]
        public void A_replacement_outside_of_the_text_is_dropped()
        {
            var changed = ReplaceFileTextApplier.Apply(
                "short",
                new[] { new ReplaceFileTextEdit("f", new TextSpan(3, 10), "x") }
                );

            Assert.Equal("short", changed);
        }

        [Fact]
        public async Task The_bom_and_the_line_breaks_of_the_file_stay()
        {
            using var solution = new TestSolution();

            var path = Path.Combine(solution.SolutionFolder, "app.config");
            var text = "<configuration>\r\n  <add type=\"Legacy.Models.Customer, Lib\" />\r\n\t<add key=\"x\" />\n</configuration>\r\n";

            File.WriteAllText(path, text, new UTF8Encoding(true));

            var edits = new EditSet();
            edits.ReplaceFileText(path, SpanOf(text, Name), "Lib.Models.Customer");

            await new EditApplier(solution.Workspace, new ClosedXamlBodyProviderFactory()).ApplyAsync(edits);

            var bytes = File.ReadAllBytes(path);

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
            Assert.Equal(
                text.Replace(Name, "Lib.Models.Customer"),
                new UTF8Encoding(true).GetString(bytes, 3, bytes.Length - 3)
                );
        }

        [Fact]
        public async Task A_file_without_a_bom_gets_none()
        {
            using var solution = new TestSolution();

            var path = Path.Combine(solution.SolutionFolder, "appsettings.json");
            var text = "{ \"type\": \"Legacy.Models.Customer\" }";

            File.WriteAllText(path, text, new UTF8Encoding(false));

            var edits = new EditSet();
            edits.ReplaceFileText(path, SpanOf(text, Name), "Lib.Models.Customer");

            await new EditApplier(solution.Workspace, new ClosedXamlBodyProviderFactory()).ApplyAsync(edits);

            var bytes = File.ReadAllBytes(path);

            Assert.NotEqual(0xEF, bytes[0]);
            Assert.Equal("{ \"type\": \"Lib.Models.Customer\" }", Encoding.UTF8.GetString(bytes));
        }

        [Fact]
        public async Task A_file_in_a_utf16_encoding_stays_in_it()
        {
            using var solution = new TestSolution();

            var path = Path.Combine(solution.SolutionFolder, "legacy.config");
            var text = "<a type=\"Legacy.Models.Customer\" />";

            File.WriteAllText(path, text, Encoding.Unicode);

            var edits = new EditSet();
            edits.ReplaceFileText(path, SpanOf(text, Name), "Lib.Models.Customer");

            await new EditApplier(solution.Workspace, new ClosedXamlBodyProviderFactory()).ApplyAsync(edits);

            var bytes = File.ReadAllBytes(path);

            Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes.Take(2).ToArray());
            Assert.Equal("<a type=\"Lib.Models.Customer\" />", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
        }

        [Fact]
        public async Task A_file_with_nothing_changed_is_not_written()
        {
            using var solution = new TestSolution();

            var path = Path.Combine(solution.SolutionFolder, "app.config");
            File.WriteAllText(path, "<a/>");
            var written = File.GetLastWriteTimeUtc(path);

            System.Threading.Thread.Sleep(30);

            var edits = new EditSet();
            edits.ReplaceFileText(path, new TextSpan(0, 0), string.Empty);

            await new EditApplier(solution.Workspace, new ClosedXamlBodyProviderFactory()).ApplyAsync(edits);

            Assert.Equal(written, File.GetLastWriteTimeUtc(path));
        }

        [Fact]
        public async Task A_file_edit_needs_a_body_provider()
        {
            using var solution = new TestSolution();

            var edits = new EditSet();
            edits.ReplaceFileText(Path.Combine(solution.SolutionFolder, "app.config"), new TextSpan(0, 1), "x");

            await Assert.ThrowsAsync<System.InvalidOperationException>(
                () => new EditApplier(solution.Workspace).ApplyAsync(edits)
                );
        }

        [Fact]
        public void The_edit_is_a_text_reference_and_equal_edits_are_one()
        {
            var edit = new ReplaceFileTextEdit("f", new TextSpan(1, 2), "x");

            Assert.Equal(EditKind.TextReference, EditKinds.KindOf(edit));

            var edits = new EditSet();
            edits.ReplaceFileText("f", new TextSpan(1, 2), "x");
            edits.ReplaceFileText("f", new TextSpan(1, 2), "x");

            Assert.Single(edits.EditsOf("f"));
        }
    }
}
