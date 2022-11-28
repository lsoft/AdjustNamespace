using System;
using System.Collections.Generic;
using System.Text;

namespace AdjustNamespace.Xaml.BodyProvider
{
    public interface IXamlBodyProvider
    {
        string XamlFilePath
        {
            get;
        }

        string ReadText();


        void UpdateText(string text);
    }
}
