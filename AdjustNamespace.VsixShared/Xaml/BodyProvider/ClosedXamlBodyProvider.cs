using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdjustNamespace.Xaml.BodyProvider
{
    /// <summary>
    /// Body provider which works with the xaml file directly through the file system.
    /// It is fast, but the changes made this way cannot be undone by the user.
    /// </summary>
    public sealed class ClosedXamlBodyProvider : IXamlBodyProvider
    {
        private static readonly UTF8Encoding _utf8WithoutBom = new UTF8Encoding(false);

        /// <summary>
        /// Encoding of the file, determined by <see cref="ReadText"/> and used to write
        /// the file back. Visual Studio saves the xaml files as UTF-8 with a byte order mark,
        /// and the mark has to survive our modification.
        /// </summary>
        private Encoding _encoding = _utf8WithoutBom;

        /// <inheritdoc/>
        public string XamlFilePath
        {
            get;
        }

        public ClosedXamlBodyProvider(
            string xamlFilePath
            )
        {
            if (xamlFilePath is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePath));
            }

            XamlFilePath = xamlFilePath;
        }

        /// <inheritdoc/>
        public string ReadText()
        {
            var bytes = File.ReadAllBytes(XamlFilePath);

            _encoding = DetectEncoding(bytes);

            var preambleLength = _encoding.GetPreamble().Length;

            return _encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
        }

        /// <inheritdoc/>
        public void UpdateText(string text)
        {
            File.WriteAllText(XamlFilePath, text, _encoding);
        }

        /// <summary>
        /// Determine the encoding of the file by its byte order mark.
        /// A file without a mark is an UTF-8 one and must not get a mark from us.
        /// </summary>
        private static Encoding DetectEncoding(byte[] bytes)
        {
            //UTF-32 LE has to be checked before UTF-16 LE: its preamble starts with the same bytes
            var encodings = new[]
            {
                Encoding.UTF8,
                Encoding.UTF32,
                Encoding.Unicode,
                Encoding.BigEndianUnicode
            };

            foreach (var encoding in encodings)
            {
                if (StartsWithPreambleOf(bytes, encoding))
                {
                    return encoding;
                }
            }

            return _utf8WithoutBom;
        }

        private static bool StartsWithPreambleOf(byte[] bytes, Encoding encoding)
        {
            var preamble = encoding.GetPreamble();
            if (preamble.Length == 0 || bytes.Length < preamble.Length)
            {
                return false;
            }

            for (var i = 0; i < preamble.Length; i++)
            {
                if (bytes[i] != preamble[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
