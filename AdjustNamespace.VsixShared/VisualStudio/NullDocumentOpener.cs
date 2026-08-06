using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// Opens nothing. This is what the extension works with when the user has not asked for
    /// the undoable changes, and what the tests work with (there is no editor there at all).
    /// </summary>
    public sealed class NullDocumentOpener : IDocumentOpener
    {
        /// <inheritdoc/>
        public Task OpenAsync(string filePath)
        {
            return Task.CompletedTask;
        }
    }
}
