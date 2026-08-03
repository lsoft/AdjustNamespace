using System;
using System.Collections.Generic;
using System.Text;

namespace AdjustNamespace.Xaml.BodyProvider
{
    /// <summary>
    /// Reader/writer of the xaml file body.
    /// </summary>
    public interface IXamlBodyProvider
    {
        /// <summary>
        /// Full path to the xaml file.
        /// </summary>
        string XamlFilePath
        {
            get;
        }

        /// <summary>
        /// Read the whole body of the xaml file.
        /// </summary>
        string ReadText();

        /// <summary>
        /// Replace the whole body of the xaml file.
        /// </summary>
        void UpdateText(string text);
    }
}
