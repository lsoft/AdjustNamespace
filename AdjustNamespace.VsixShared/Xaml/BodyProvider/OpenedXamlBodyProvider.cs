using AdjustNamespace.Helper;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace AdjustNamespace.Xaml.BodyProvider
{
    public sealed class OpenedXamlBodyProvider : IXamlBodyProvider
    {
        private readonly VsServices _vss;
        private DocumentView? _dw;

        public string XamlFilePath
        {
            get;
        }


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

        public async Task OpenAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            //var (r, subjectProject, subjectProjectItem) = await SolutionHelper.TryGetProjectItemAsync(XamlFilePath);

            //if (!r)
            //{
            //    throw new InvalidOperationException($"File {XamlFilePath} not found!");
            //}

            _dw = await VS.Documents.OpenViaProjectAsync(XamlFilePath);
        }

        public string ReadText()
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (_dw == null)
            {
                throw new InvalidOperationException("Please open document before retrieving its text");
            }

            var result = _dw!.TextView!.TextSnapshot.GetText();
            return result;
        }

        public void UpdateText(string text)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (_dw == null)
            {
                throw new InvalidOperationException("Please open document before saving its text");
            }

            var edit = _dw!.TextView!.TextBuffer.CreateEdit();
            edit.Delete(0, edit.Snapshot.GetText().Length);
            edit.Insert(0, text);
            edit.Apply();
        }
    }
}
