using System;
using System.Collections.Generic;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// A small positioned reader of xml: it hands out the attribute values, the text of the
    /// elements and the comments with the exact span they are written at.
    ///
    /// <c>XmlReader</c> knows the line and the column of a node, but not where the value of an
    /// attribute starts and where it ends in the text, and a file has to be changed where it
    /// is written (never written back through a DOM). This reader is as tolerant as a text
    /// search has to be: a config file which is not a well formed xml (a template, a half
    /// edited file) is read as far as it goes.
    /// </summary>
    internal static class XmlTokenizer
    {
        public static List<XmlPiece> Tokenize(string text)
        {
            var result = new List<XmlPiece>();
            var elements = new Stack<string>();

            var i = 0;
            while (i < text.Length)
            {
                if (text[i] != '<')
                {
                    var end = text.IndexOf('<', i);
                    if (end < 0)
                    {
                        end = text.Length;
                    }

                    if (elements.Count > 0 && !IsBlank(text, i, end))
                    {
                        result.Add(new XmlPiece(XmlPieceKind.ElementText, elements.Peek(), null, i, end - i));
                    }

                    i = end;
                    continue;
                }

                if (StartsWith(text, i, "<!--"))
                {
                    var end = text.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    var contentEnd = end < 0 ? text.Length : end;

                    result.Add(new XmlPiece(XmlPieceKind.Comment, elements.Count > 0 ? elements.Peek() : string.Empty, null, i + 4, contentEnd - i - 4));

                    i = end < 0 ? text.Length : end + 3;
                    continue;
                }

                if (StartsWith(text, i, "<![CDATA["))
                {
                    var end = text.IndexOf("]]>", i, StringComparison.Ordinal);
                    i = end < 0 ? text.Length : end + 3;
                    continue;
                }

                if (StartsWith(text, i, "<?"))
                {
                    var end = text.IndexOf("?>", i, StringComparison.Ordinal);
                    i = end < 0 ? text.Length : end + 2;
                    continue;
                }

                if (StartsWith(text, i, "<!"))
                {
                    i = SkipDeclaration(text, i);
                    continue;
                }

                if (StartsWith(text, i, "</"))
                {
                    var end = text.IndexOf('>', i);
                    if (elements.Count > 0)
                    {
                        elements.Pop();
                    }

                    i = end < 0 ? text.Length : end + 1;
                    continue;
                }

                i = ReadStartTag(text, i, elements, result);
            }

            return result;
        }

        /// <summary>
        /// Read <c>&lt;name attr="value" ...&gt;</c> or the self closing form.
        /// </summary>
        /// <returns>The position behind the tag.</returns>
        private static int ReadStartTag(
            string text,
            int tagStart,
            Stack<string> elements,
            List<XmlPiece> result
            )
        {
            var i = tagStart + 1;

            var nameStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '>' && text[i] != '/')
            {
                i++;
            }

            var elementName = text.Substring(nameStart, i - nameStart);

            if (elementName.Length == 0)
            {
                //a stray `<`
                return tagStart + 1;
            }

            var selfClosing = false;

            while (i < text.Length)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                if (i >= text.Length)
                {
                    break;
                }

                if (text[i] == '>')
                {
                    i++;
                    break;
                }

                if (text[i] == '/')
                {
                    selfClosing = i + 1 < text.Length && text[i + 1] == '>';
                    i += selfClosing ? 2 : 1;

                    if (selfClosing)
                    {
                        break;
                    }

                    continue;
                }

                var attributeStart = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '=' && text[i] != '>' && text[i] != '/')
                {
                    i++;
                }

                var attributeName = text.Substring(attributeStart, i - attributeStart);

                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                if (i >= text.Length || text[i] != '=')
                {
                    if (attributeName.Length == 0)
                    {
                        i++;
                    }

                    continue;
                }

                i++;

                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                if (i >= text.Length || (text[i] != '"' && text[i] != '\''))
                {
                    continue;
                }

                var quote = text[i];
                var valueStart = i + 1;
                var valueEnd = text.IndexOf(quote, valueStart);
                if (valueEnd < 0)
                {
                    valueEnd = text.Length;
                }

                result.Add(new XmlPiece(XmlPieceKind.AttributeValue, elementName, attributeName, valueStart, valueEnd - valueStart));

                i = Math.Min(valueEnd + 1, text.Length);
            }

            if (!selfClosing)
            {
                elements.Push(elementName);
            }

            return i;
        }

        /// <summary>
        /// <c>&lt;!DOCTYPE ...&gt;</c> with its internal subset, if any.
        /// </summary>
        private static int SkipDeclaration(string text, int start)
        {
            var depth = 0;

            for (var i = start; i < text.Length; i++)
            {
                if (text[i] == '[')
                {
                    depth++;
                }
                else if (text[i] == ']')
                {
                    depth--;
                }
                else if (text[i] == '>' && depth <= 0)
                {
                    return i + 1;
                }
            }

            return text.Length;
        }

        private static bool StartsWith(string text, int position, string prefix)
        {
            return string.CompareOrdinal(text, position, prefix, 0, prefix.Length) == 0;
        }

        private static bool IsBlank(string text, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
