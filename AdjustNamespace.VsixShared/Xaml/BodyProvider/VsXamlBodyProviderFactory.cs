using System;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml.BodyProvider
{
    /// <summary>
    /// Factory used by the extension itself: reads go through the file system (fast probes),
    /// writes go through an invisible text buffer so the change participates in the global
    /// linked undo transaction without opening an editor tab.
    /// </summary>
    public sealed class VsXamlBodyProviderFactory : IXamlBodyProviderFactory
    {
        /// <inheritdoc/>
        public Task<IXamlBodyProvider> CreateForReadAsync(string xamlFilePath)
        {
            if (xamlFilePath is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePath));
            }

            return Task.FromResult<IXamlBodyProvider>(
                new ClosedXamlBodyProvider(xamlFilePath)
                );
        }

        /// <inheritdoc/>
        public async Task<IXamlBodyProvider> CreateForWriteAsync(string xamlFilePath)
        {
            if (xamlFilePath is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePath));
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            return InvisibleXamlBodyProvider.Open(xamlFilePath);
        }
    }
}
