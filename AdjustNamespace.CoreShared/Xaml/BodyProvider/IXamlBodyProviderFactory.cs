using System.Threading.Tasks;

namespace AdjustNamespace.Xaml.BodyProvider
{
    /// <summary>
    /// Source of the xaml body a <see cref="XamlDocument"/> is built over: the file system
    /// or the editor of Visual Studio.
    ///
    /// This is the second place (besides <see cref="AdjustNamespace.VisualStudio.IDocumentOpener"/>)
    /// where an undoable change needs the IDE: a xaml file changed through the text buffer of an
    /// opened document may be undone by the user, a xaml file rewritten on the disk may not.
    /// Everything outside of Visual Studio (the console utility, the tests) works with the file
    /// system only, see <see cref="ClosedXamlBodyProviderFactory"/>.
    /// </summary>
    public interface IXamlBodyProviderFactory
    {
        /// <summary>
        /// Create a reader/writer of the given xaml file.
        /// </summary>
        /// <param name="openFilesToEnableUndo">
        /// Open the file in the editor instead of reading it from the disk. This is a wish of the
        /// user (see the second step of the wizard) and not a fact about the environment, so an
        /// implementation which has no editor at all simply ignores it.
        /// </param>
        /// <param name="xamlFilePath">Full path to the xaml file.</param>
        Task<IXamlBodyProvider> CreateAsync(
            bool openFilesToEnableUndo,
            string xamlFilePath
            );
    }
}
