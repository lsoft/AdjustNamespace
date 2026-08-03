using EnvDTE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Extension for file from workspace.
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

        /// <summary>
        /// Full path to the project the file belongs to.
        /// </summary>
        public readonly string ProjectPath;

        public FileEx(
            string filePath,
            string projectPath
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (projectPath is null)
            {
                throw new ArgumentNullException(nameof(projectPath));
            }

            var fi = new FileInfo(filePath);
            FolderPath = fi.Directory.FullName;
            FileName = fi.Name;
            FilePath = filePath;
            ProjectPath = projectPath;
        }

    }
}
