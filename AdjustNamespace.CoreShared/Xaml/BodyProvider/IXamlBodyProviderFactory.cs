using System.Threading.Tasks;

namespace AdjustNamespace.Xaml.BodyProvider
{
    /// <summary>
    /// Source of the xaml body a <see cref="XamlDocument"/> is built over: the file system
    /// or an (invisible) Visual Studio text buffer.
    ///
    /// A change written through a text buffer participates in a global linked undo
    /// transaction and is therefore undoable with Ctrl+Z; a change written to the disk is not.
    /// Everything outside of Visual Studio (the console utility, the tests) works with the
    /// file system only, see <see cref="ClosedXamlBodyProviderFactory"/>.
    /// </summary>
    public interface IXamlBodyProviderFactory
    {
        /// <summary>
        /// Create a reader of the given xaml file. Never opens an editor: used to probe
        /// whether a file would change before touching it for real.
        /// </summary>
        Task<IXamlBodyProvider> CreateForReadAsync(string xamlFilePath);

        /// <summary>
        /// Create a reader/writer of the given xaml file for applying a change.
        /// In Visual Studio this opens an invisible text buffer so the change is undoable;
        /// elsewhere it is the file system.
        /// </summary>
        Task<IXamlBodyProvider> CreateForWriteAsync(string xamlFilePath);
    }
}
