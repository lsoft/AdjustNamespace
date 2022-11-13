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
        public readonly string FolderPath;
        public readonly string FileName;
        public readonly string FilePath;
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
