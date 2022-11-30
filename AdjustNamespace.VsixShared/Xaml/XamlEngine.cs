using AdjustNamespace.Helper;
using AdjustNamespace.Xaml.BodyProvider;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;

namespace AdjustNamespace.Xaml
{
    public class XamlEngine
    {
        private readonly VsServices _vss;

        public XamlEngine(
            VsServices vss
            )
        {
            _vss = vss;
        }

        public async System.Threading.Tasks.Task<XamlDocument> CreateDocumentAsync(
            bool openFilesToEnableUndo,
            string xamlFilePath
            )
        {
            var bodyProvider = await CreateBodyProviderAsync(openFilesToEnableUndo, xamlFilePath);

            return new XamlDocument(bodyProvider);
        }

        private async System.Threading.Tasks.Task<IXamlBodyProvider> CreateBodyProviderAsync(
            bool openFilesToEnableUndo,
            string xamlFilePath
            )
        {
            IXamlBodyProvider bodyProvider;
            if (openFilesToEnableUndo)
            {
                var obp = new OpenedXamlBodyProvider(_vss, xamlFilePath);
                await obp.OpenAsync();

                bodyProvider = obp;
            }
            else
            {
                bodyProvider = new ClosedXamlBodyProvider(xamlFilePath);
            }

            return bodyProvider;
        }
    }
}
