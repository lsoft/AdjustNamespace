using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdjustNamespace.VsixShared.Settings
{
    public class AdjustNamespaceSettings2
    {
        private readonly string _solutionFolder;
        
        public readonly AdjustNamespaceSettings Settings;

        public AdjustNamespaceSettings2(
            string solutionFolder,
            AdjustNamespaceSettings settings
            )
        {
            if (solutionFolder is null)
            {
                throw new ArgumentNullException(nameof(solutionFolder));
            }

            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            _solutionFolder = solutionFolder;
            Settings = settings;
        }

        public bool IsSkippedFolder(string fullFolderPath)
        {
            fullFolderPath = Path.GetFullPath(fullFolderPath)
                .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var sfs in Settings.SkippedFolderSuffixes)
            {
                if (Path.IsPathRooted(sfs))
                {
                    var rsfs = Path.GetFullPath(sfs)
                        .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (rsfs == fullFolderPath)
                    {
                        return true;
                    }
                }
                else
                {
                    var rsfs = Path.Combine(_solutionFolder, sfs)
                        .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (rsfs == fullFolderPath)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

    }
}
