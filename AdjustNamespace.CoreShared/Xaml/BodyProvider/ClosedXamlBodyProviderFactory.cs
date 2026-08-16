using System;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml.BodyProvider
{
    /// <summary>
    /// Factory which always works with the file system, see <see cref="ClosedXamlBodyProvider"/>.
    /// This is what everything without a Visual Studio editor behind it uses.
    /// </summary>
    public sealed class ClosedXamlBodyProviderFactory : IXamlBodyProviderFactory
    {
        /// <inheritdoc/>
        public Task<IXamlBodyProvider> CreateAsync(
            bool openFilesToEnableUndo,
            string xamlFilePath
            )
        {
            if (xamlFilePath is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePath));
            }

            return Task.FromResult<IXamlBodyProvider>(
                new ClosedXamlBodyProvider(xamlFilePath)
                );
        }
    }
}
