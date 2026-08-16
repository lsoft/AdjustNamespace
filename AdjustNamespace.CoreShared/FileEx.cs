using System;
using System.IO;

namespace AdjustNamespace
{
    /// <summary>
    /// A file of the solution as the file tree of the wizard shows it.
    /// </summary>
    public readonly struct FileEx
    {
        /// <summary>
        /// Folder the file lives in. The files are grouped by it in the wizard.
        /// </summary>
        public readonly string FolderPath;

        /// <summary>
        /// Name of the file (without the folder).
        /// </summary>
        public readonly string FileName;

        /// <summary>
        /// Full path to the file.
        /// </summary>
        public readonly string FilePath;

        public FileEx(
            string filePath
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            var fi = new FileInfo(filePath);
            FolderPath = fi.Directory!.FullName;
            FileName = fi.Name;
            FilePath = filePath;
        }

    }
}
