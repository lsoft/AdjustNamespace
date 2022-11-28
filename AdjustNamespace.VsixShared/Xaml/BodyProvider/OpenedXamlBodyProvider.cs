using AdjustNamespace.Helper;
using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdjustNamespace.Xaml.BodyProvider
{
    public sealed class OpenedXamlBodyProvider : IXamlBodyProvider
    {
        private readonly VsServices _vss;
        
        public string XamlFilePath
        {
            get;
        }

        private EnvDTE.Window? _w;

        public OpenedXamlBodyProvider(
            VsServices vss,
            string xamlFilePath
            )
        {
            if (xamlFilePath is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePath));
            }
            _vss = vss;
            XamlFilePath = xamlFilePath;
        }

        public void Open()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (!_vss.Dte.Solution.TryGetProjectItem(XamlFilePath, out var subjectProject, out var subjectProjectItem))
            {
                throw new InvalidOperationException($"File {XamlFilePath} not found!");
            }

            var w = subjectProjectItem!.Open();
            w.Visible = true;
            //no need for this: w.Activate();

            _w = w;
        }

        public string ReadText()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (_w == null)
            {
                throw new InvalidOperationException("Please open document before retrieving its text");
            }

            var textDocument = (TextDocument)_w.Document.Object("TextDocument");
            var textSelection = textDocument.Selection;
            textSelection.SelectAll();
            var t = textSelection.Text;

            return t;
        }

        public void UpdateText(string text)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (_w == null)
            {
                throw new InvalidOperationException("Please open document before saving its text");
            }

            var textDocument = (TextDocument)_w.Document.Object("TextDocument");
            var textSelection = textDocument.Selection;
            textSelection.SelectAll();
            textSelection.Insert(text);
        }
    }
}
