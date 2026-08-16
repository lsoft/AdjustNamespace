using AdjustNamespace.Xaml.BodyProvider;
using System;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml
{
    /// <summary>
    /// Factory of <see cref="XamlDocument"/>.
    /// It hides the difference between a xaml file opened in the Visual Studio editor
    /// and a closed one (see <see cref="IXamlBodyProviderFactory"/>).
    /// </summary>
    public class XamlEngine
    {
        private readonly IXamlBodyProviderFactory _bodyProviderFactory;

        public XamlEngine(
            IXamlBodyProviderFactory bodyProviderFactory
            )
        {
            if (bodyProviderFactory is null)
            {
                throw new ArgumentNullException(nameof(bodyProviderFactory));
            }

            _bodyProviderFactory = bodyProviderFactory;
        }

        /// <summary>
        /// Read the xaml file and parse its structure.
        /// </summary>
        /// <param name="openFilesToEnableUndo">
        /// Open the file in the Visual Studio editor and work with its text buffer.
        /// This makes the changes undoable by the user, but slows the processing down.
        /// Implementations without an editor ignore the flag.
        /// </param>
        /// <param name="xamlFilePath">Full path to the xaml file.</param>
        public async Task<XamlDocument> CreateDocumentAsync(
            bool openFilesToEnableUndo,
            string xamlFilePath
            )
        {
            var bodyProvider = await _bodyProviderFactory.CreateAsync(
                openFilesToEnableUndo,
                xamlFilePath
                );

            return new XamlDocument(bodyProvider);
        }
    }
}
