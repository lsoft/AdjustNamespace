using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AdjustNamespace.Settings
{
    /// <summary>
    /// The solution settings (<see cref="AdjustNamespaceSettings"/>) bound to the folder
    /// of that solution. The binding is required to resolve the relative paths of the settings.
    /// </summary>
    public class AdjustNamespaceSettings2
    {
        private readonly string _solutionFolder;

        /// <summary>
        /// The settings themselves (as they are stored in the settings file).
        /// </summary>
        public readonly AdjustNamespaceSettings Settings;

        /// <param name="solutionFolder">Folder of the solution the settings belong to.</param>
        /// <param name="settings">The settings themselves.</param>
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

        /// <summary>
        /// Check if the given folder is excluded from the namespace chain by the user.
        /// Both the rooted and the solution-relative paths of the settings are supported.
        /// </summary>
        /// <param name="fullFolderPath">Full path to the folder to check.</param>
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
