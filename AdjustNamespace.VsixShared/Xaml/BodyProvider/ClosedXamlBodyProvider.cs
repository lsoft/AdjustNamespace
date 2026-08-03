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
            var result = File.ReadAllText(XamlFilePath);

            return result;
        }

        /// <inheritdoc/>
        public void UpdateText(string text)
        {
            File.WriteAllText(XamlFilePath, text);
        }
    }
}
