using System;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml.BodyProvider
{
    /// <summary>
    /// Factory which is able to work with the xaml file through the Visual Studio editor,
    /// see <see cref="OpenedXamlBodyProvider"/>. This is what the extension itself uses:
    /// the changes of a file opened in the editor are undoable by the user.
    ///
    /// Opening is slow, so it happens only if the user has asked for it
    /// (see the second step of the wizard).
    /// </summary>
    public sealed class VsXamlBodyProviderFactory : IXamlBodyProviderFactory
    {
        /// <inheritdoc/>
        public async Task<IXamlBodyProvider> CreateAsync(
            bool openFilesToEnableUndo,
            string xamlFilePath
            )
        {
            if (xamlFilePath is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePath));
            }

            if (!openFilesToEnableUndo)
            {
                return new ClosedXamlBodyProvider(xamlFilePath);
            }

            var provider = new OpenedXamlBodyProvider(xamlFilePath);
            await provider.OpenAsync();

            return provider;
        }
    }
}
