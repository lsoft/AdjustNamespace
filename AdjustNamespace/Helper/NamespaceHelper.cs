using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Helper
{
    public static class NamespaceHelper
    {
        public static string GetTargetNamespace(
            this Microsoft.CodeAnalysis.Project project,
            string documentFilePath
            )
        {
            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (documentFilePath is null)
            {
                throw new ArgumentNullException(nameof(documentFilePath));
            }

            var projectFolderPath = new FileInfo(project.FilePath).Directory.FullName;
            var suffix = new FileInfo(documentFilePath).Directory.FullName.Substring(projectFolderPath.Length);
            var targetNamespace = project.DefaultNamespace +
                suffix
                    .Replace(Path.DirectorySeparatorChar, '.')
                    .Replace(Path.AltDirectorySeparatorChar, '.')
                    ;

            return targetNamespace;
        }
    }
}
