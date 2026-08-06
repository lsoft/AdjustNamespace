using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// Opener of a file in the Visual Studio editor.
    ///
    /// The extension changes the documents through the Roslyn workspace, and such a change is
    /// undoable by the user (Ctrl+Z) only if the document is opened in the editor. Opening is
    /// slow, so it is a user option (`open affected files to enable Undo`) and not the default.
    /// </summary>
    public interface IDocumentOpener
    {
        /// <summary>
        /// Open the given file in the editor.
        /// </summary>
        Task OpenAsync(string filePath);
    }
}
