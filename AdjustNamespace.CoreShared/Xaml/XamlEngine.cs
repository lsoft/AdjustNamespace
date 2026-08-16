using AdjustNamespace.Xaml.BodyProvider;
using System;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml
{
    /// <summary>
    /// Factory of <see cref="XamlDocument"/>.
    /// It hides the difference between a xaml file edited through an (invisible) text buffer
    /// and one rewritten on the disk (see <see cref="IXamlBodyProviderFactory"/>).
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
        /// Read the xaml file for a probe (no editor is opened).
        /// </summary>
        public async Task<XamlDocument> CreateForReadAsync(string xamlFilePath)
        {
            var bodyProvider = await _bodyProviderFactory.CreateForReadAsync(xamlFilePath);
            return new XamlDocument(bodyProvider);
        }

        /// <summary>
        /// Read the xaml file for applying a change. In Visual Studio the body is an invisible
        /// text buffer so the write is undoable; elsewhere it is the file on disk.
        /// Dispose the document's body provider after saving (or when abandoning the write).
        /// </summary>
        public async Task<XamlDocument> CreateForWriteAsync(string xamlFilePath)
        {
            var bodyProvider = await _bodyProviderFactory.CreateForWriteAsync(xamlFilePath);
            return new XamlDocument(bodyProvider);
        }
    }
}
