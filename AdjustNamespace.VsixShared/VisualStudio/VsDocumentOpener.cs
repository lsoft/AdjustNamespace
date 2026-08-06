using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// Opens the file in the editor of the running Visual Studio.
    /// </summary>
    public sealed class VsDocumentOpener : IDocumentOpener
    {
        /// <inheritdoc/>
        public async Task OpenAsync(string filePath)
        {
            _ = await VS.Documents.OpenAsync(filePath);
        }
    }
}
