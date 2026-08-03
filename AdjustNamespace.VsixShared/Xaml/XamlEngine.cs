using AdjustNamespace.Helper;
using AdjustNamespace.Xaml.BodyProvider;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;

namespace AdjustNamespace.Xaml
{
    /// <summary>
    /// Factory of <see cref="XamlDocument"/>.
    /// It hides the difference between a xaml file opened in the Visual Studio editor
    /// and a closed one (see <c>AdjustNamespace.Xaml.BodyProvider</c> namespace).
    /// </summary>
    public class XamlEngine
    {
        private readonly VsServices _vss;

        /// <param name="vss">Visual Studio services.</param>
        public XamlEngine(
            VsServices vss
            )
        {
            _vss = vss;
        }

        /// <summary>
        /// Read the xaml file and parse its structure.
        /// </summary>
        /// <param name="openFilesToEnableUndo">
        /// Open the file in the Visual Studio editor and work with its text buffer.
        /// This makes the changes undoable by the user, but slows the processing down.
        /// </param>
        /// <param name="xamlFilePath">Full path to the xaml file.</param>
        public async System.Threading.Tasks.Task<XamlDocument> CreateDocumentAsync(
            bool openFilesToEnableUndo,
            string xamlFilePath
            )
        {
            var bodyProvider = await CreateBodyProviderAsync(openFilesToEnableUndo, xamlFilePath);

            return new XamlDocument(bodyProvider);
        }

        /// <summary>
        /// Create a reader/writer of the xaml file body: either through the editor
        /// (<see cref="OpenedXamlBodyProvider"/>) or through the file system
        /// (<see cref="ClosedXamlBodyProvider"/>).
        /// </summary>
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
