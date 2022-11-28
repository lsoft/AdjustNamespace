using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdjustNamespace.Xaml.BodyProvider
{
    public sealed class ClosedXamlBodyProvider : IXamlBodyProvider
    {
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

        public string ReadText()
        {
            var result = File.ReadAllText(XamlFilePath);

            return result;
        }

        public void UpdateText(string text)
        {
            File.WriteAllText(XamlFilePath, text);
        }
    }
}
