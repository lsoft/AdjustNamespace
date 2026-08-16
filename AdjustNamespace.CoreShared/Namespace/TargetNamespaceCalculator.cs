using AdjustNamespace.Settings;
using System;
using System.Collections.Generic;
using System.IO;

namespace AdjustNamespace.Namespace
{
    /// <summary>
    /// The rule which turns the location of a file into the namespace its types have to live in:
    /// the default namespace of the project, plus the folders between the project folder and
    /// the file, modified with the user defined regex.
    ///
    /// Everything here is a computation over the paths as strings: no file system, no Roslyn
    /// and no Visual Studio, hence no main thread (<see cref="FileInfo"/> and
    /// <see cref="DirectoryInfo"/> are used as the parsers of a path and never touch the disk).
    /// The only part of the rule which needs Visual Studio is the default namespace of the
    /// project itself, see <see cref="Helper.IProjectDefaultNamespaceProvider"/>: it is resolved
    /// by the caller and comes in as a parameter.
    /// </summary>
    public static class TargetNamespaceCalculator
    {
        /// <summary>
        /// The folders between the folder of the project and the folder of the file,
        /// from the outermost one to the innermost one. The folders excluded by the user
        /// (see <see cref="AdjustNamespaceSettings2.IsSkippedFolder"/>) are left out, while
        /// their subfolders stay: excluding <c>Impl</c> of <c>MyApp\Impl\Details</c> gives
        /// <c>MyApp.Details</c>.
        /// </summary>
        /// <param name="projectFilePath">Full path to the project file (<c>...\MyApp\MyApp.csproj</c>).</param>
        /// <param name="documentFilePath">Full path to the file.</param>
        /// <param name="settings">Settings of the solution, they know the excluded folders.</param>
        /// <returns>
        /// The chain of the folder names (an empty list for a file which lies in the project
        /// folder itself), or <c>null</c> if the file is outside of the project folder
        /// (a linked file, for example): such a file has no target namespace at all.
        /// </returns>
        public static IReadOnlyList<string>? TryGetFolderChain(
            string projectFilePath,
            string documentFilePath,
            AdjustNamespaceSettings2 settings
            )
        {
            if (projectFilePath is null)
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }

            if (documentFilePath is null)
            {
                throw new ArgumentNullException(nameof(documentFilePath));
            }

            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var projectFolderPath = new FileInfo(projectFilePath).Directory!.FullName;
            var documentFolderPath = new FileInfo(documentFilePath).Directory!.FullName;

            if (!IsSameFolderOrBelow(documentFolderPath, projectFolderPath))
            {
                return null;
            }

            //collect the folder names from the file folder up to the project folder
            var names = new List<string>();
            var dir = new DirectoryInfo(documentFolderPath);
            while (!string.Equals(dir.FullName, projectFolderPath, StringComparison.OrdinalIgnoreCase)
                && dir.FullName.Length > projectFolderPath.Length)
            {
                if (!settings.IsSkippedFolder(dir.FullName))
                {
                    names.Add(dir.Name);
                }

                dir = dir.Parent!;
            }

            names.Reverse();

            return names;
        }

        /// <summary>
        /// The folder is the given one or lies inside it.
        ///
        /// The comparison has to stop at the folder border: a plain <c>StartsWith</c> also
        /// accepts a sibling folder whose name merely begins with the name of this one
        /// (<c>...\MyApp.Tests\Sub</c> starts with <c>...\MyApp</c>), and a linked file of
        /// such a folder would get the whole sibling folder into its namespace
        /// (<c>MyApp.MyApp.Tests.Sub</c>) instead of being left alone.
        ///
        /// The comparison is case insensitive: Windows paths are, and Roslyn and the IDE
        /// may hand the same folder over in a different case.
        /// </summary>
        private static bool IsSameFolderOrBelow(
            string folderPath,
            string rootFolderPath
            )
        {
            if (folderPath.Length < rootFolderPath.Length)
            {
                return false;
            }

            if (!folderPath.StartsWith(rootFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (folderPath.Length == rootFolderPath.Length)
            {
                //the file lies in the root folder itself
                return true;
            }

            //a root folder written with a trailing separator is a border already
            if (IsSeparator(rootFolderPath[rootFolderPath.Length - 1]))
            {
                return true;
            }

            return IsSeparator(folderPath[rootFolderPath.Length]);
        }

        private static bool IsSeparator(char c)
        {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }

        /// <summary>
        /// Build the target namespace out of the default namespace of the project and the
        /// folder chain of the file (see <see cref="TryGetFolderChain"/>), and apply the user
        /// defined regex to the result.
        ///
        /// The regex is applied to the whole name and not to its parts: this is what the user
        /// sees on the second step of the wizard.
        /// </summary>
        /// <param name="projectDefaultNamespace">Default (root) namespace of the project.</param>
        /// <param name="folderChain">Folders between the project folder and the file.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        public static string Compose(
            string projectDefaultNamespace,
            IReadOnlyList<string> folderChain,
            NamespaceReplaceRegex replaceRegex
            )
        {
            if (projectDefaultNamespace is null)
            {
                throw new ArgumentNullException(nameof(projectDefaultNamespace));
            }

            if (folderChain is null)
            {
                throw new ArgumentNullException(nameof(folderChain));
            }

            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            var names = new List<string>(folderChain.Count + 1)
            {
                projectDefaultNamespace
            };
            names.AddRange(folderChain);

            var targetNamespace = string.Join(".", names);

            return replaceRegex.Modify(targetNamespace);
        }

        /// <summary>
        /// The default namespace of a project Visual Studio reports nothing about
        /// (a project of a kind which has no <c>DefaultNamespace</c> property): the name of
        /// the project without its last part, <c>MyApp.Shared</c> -> <c>MyApp</c>.
        ///
        /// This is what supports the shared projects: a <c>.shproj</c> is usually named after
        /// the project which owns its code, see <c>Tests\README.md</c>.
        /// </summary>
        /// <param name="projectName">Name of the project.</param>
        public static string DefaultNamespaceFallback(
            string projectName
            )
        {
            if (projectName is null)
            {
                throw new ArgumentNullException(nameof(projectName));
            }

            var dotIndex = projectName.LastIndexOf(".", StringComparison.Ordinal);
            if (dotIndex <= 0)
            {
                //there is nothing to cut off, or the name starts with a dot
                return projectName;
            }

            return projectName.Substring(0, dotIndex);
        }
    }
}
