using AdjustNamespace.TextReferences;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// Tests of <see cref="XmlTokenizer"/>: the exact span of an attribute value, of the text of an
    /// element and of a comment, in a text that is not always a well formed xml.
    /// </summary>
    public class XmlTokenizerTests
    {
        private static string Text(string xml, XmlPiece piece)
        {
            return xml.Substring(piece.Start, piece.Length);
        }

        [Fact]
        public void An_attribute_value_has_an_exact_span_with_either_quote()
        {
            var xml = "<a x=\"one\" y='two' z = \"three\" />";

            var pieces = XmlTokenizer.Tokenize(xml);

            Assert.Equal(new[] { "one", "two", "three" }, pieces.Select(p => Text(xml, p)).ToArray());
            Assert.Equal(new[] { "x", "y", "z" }, pieces.Select(p => p.AttributeName).ToArray());
            Assert.All(pieces, p => Assert.Equal("a", p.ElementName));
        }

        [Fact]
        public void The_text_of_an_element_belongs_to_it()
        {
            var xml = "<Project>\r\n  <PropertyGroup>\r\n    <StartupObject>A.B.Program</StartupObject>\r\n  </PropertyGroup>\r\n</Project>";

            var piece = Assert.Single(XmlTokenizer.Tokenize(xml));

            Assert.Equal(XmlPieceKind.ElementText, piece.Kind);
            Assert.Equal("StartupObject", piece.ElementName);
            Assert.Equal("A.B.Program", Text(xml, piece));
        }

        [Fact]
        public void The_end_tags_pop_the_elements()
        {
            var xml = "<a><b>one</b>two</a>";

            var pieces = XmlTokenizer.Tokenize(xml);

            Assert.Equal(new[] { "b", "a" }, pieces.Select(p => p.ElementName).ToArray());
            Assert.Equal(new[] { "one", "two" }, pieces.Select(p => Text(xml, p)).ToArray());
        }

        [Fact]
        public void A_self_closing_element_has_no_text_and_does_not_open_a_scope()
        {
            var xml = "<a><b x=\"1\"/>text</a>";

            var pieces = XmlTokenizer.Tokenize(xml);

            Assert.Equal(2, pieces.Count);
            Assert.Equal("b", pieces[0].ElementName);
            Assert.Equal("a", pieces[1].ElementName);
            Assert.Equal("text", Text(xml, pieces[1]));
        }

        [Fact]
        public void A_comment_is_a_piece_and_what_is_in_it_is_not_parsed()
        {
            var xml = "<a><!-- <b x=\"1\"/> A.B.C --><c/></a>";

            var pieces = XmlTokenizer.Tokenize(xml);

            var comment = Assert.Single(pieces);

            Assert.Equal(XmlPieceKind.Comment, comment.Kind);
            Assert.Equal(" <b x=\"1\"/> A.B.C ", Text(xml, comment));
        }

        [Fact]
        public void Cdata_and_the_declaration_and_the_doctype_are_skipped()
        {
            var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><!DOCTYPE a [<!ENTITY e \"x\">]><a><![CDATA[ <b y=\"2\"/> ]]><c z=\"3\"/></a>";

            var pieces = XmlTokenizer.Tokenize(xml);

            var piece = Assert.Single(pieces);

            Assert.Equal("z", piece.AttributeName);
            Assert.Equal("3", Text(xml, piece));
        }

        [Fact]
        public void A_prefixed_name_is_kept_as_it_is()
        {
            var xml = "<ns:a ns:x=\"v\" xmlns:ns=\"urn:a\"/>";

            var pieces = XmlTokenizer.Tokenize(xml);

            Assert.Equal("ns:a", pieces[0].ElementName);
            Assert.Equal("ns:x", pieces[0].AttributeName);
            Assert.Equal("v", Text(xml, pieces[0]));
        }

        [Theory]
        [InlineData("<a x=\"unterminated")]
        [InlineData("<a x=")]
        [InlineData("<a")]
        [InlineData("<")]
        [InlineData("< a>")]
        [InlineData("<a><b></a></b>")]
        [InlineData("<!-- unterminated")]
        [InlineData("<![CDATA[ unterminated")]
        [InlineData("</a></b>")]
        [InlineData("text only <")]
        [InlineData("")]
        public void A_broken_text_is_read_as_far_as_it_goes_and_never_throws(string xml)
        {
            var pieces = XmlTokenizer.Tokenize(xml);

            Assert.All(pieces, p => Assert.True(p.Start >= 0 && p.Start + p.Length <= xml.Length));
        }

        [Fact]
        public void The_text_between_tags_that_is_blank_is_not_a_piece()
        {
            var xml = "<a>\n   \n<b/>\n</a>";

            Assert.Empty(XmlTokenizer.Tokenize(xml));
        }
    }
}
