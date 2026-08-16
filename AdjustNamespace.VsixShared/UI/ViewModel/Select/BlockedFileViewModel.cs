using AdjustNamespace.Adjusting.Plan;
using System.IO;

namespace AdjustNamespace.UI.ViewModel.Select
{
    /// <summary>
    /// A file which cannot be adjusted, shown read-only on the second wizard step.
    /// </summary>
    public sealed class BlockedFileViewModel
    {
        /// <summary>
        /// File name (without the folder).
        /// </summary>
        public string FileName
        {
            get;
        }

        /// <summary>
        /// Full path to the file.
        /// </summary>
        public string FilePath
        {
            get;
        }

        /// <summary>
        /// Why the file cannot be adjusted.
        /// </summary>
        public string Message
        {
            get;
        }

        public BlockedFileViewModel(
            AdjustBlock block
            )
        {
            FilePath = block.FilePath;
            FileName = Path.GetFileName(block.FilePath);
            Message = block.Message;
        }
    }
}
