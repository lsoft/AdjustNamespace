using EnvDTE;
using System;
using System.Collections.Generic;
using System.Text;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Extension for file from workspace.
    /// </summary>
    public readonly struct FileExtension
    {
        public readonly string FilePath;
        public readonly Project Project;
        public readonly string ProjectPath;
        public readonly ProjectItem ProjectItem;

        public FileExtension(
            string filePath,
            EnvDTE.Project project,
            ProjectItem projectItem
            )
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            FilePath = filePath;
            Project = project;
            ProjectPath = project.FullName;
            ProjectItem = projectItem;
        }

    }
}
