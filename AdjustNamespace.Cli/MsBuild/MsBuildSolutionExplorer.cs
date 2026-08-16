using AdjustNamespace.VisualStudio;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Cli.MsBuild
{
    /// <summary>
    /// The solution tree as the console utility sees it: the documents of the loaded workspace
    /// plus the xaml files which lie in the folders of the projects.
    ///
    /// Visual Studio answers these questions from the solution tree, which knows every physical
    /// file of every project. There is no such tree here: Roslyn reports the documents it
    /// compiles and nothing else, so the xaml files (which the adjusting has to fix as well)
    /// are searched for on the disk. Everything under a <c>bin</c> or an <c>obj</c> folder is
    /// a build output and is skipped.
    /// </summary>
    public sealed class MsBuildSolutionExplorer : ISolutionExplorer
    {
        private readonly Workspace _workspace;

        /// <summary>
        /// The map is built once: it walks the disk, and both questions of the interface are
        /// answered out of it.
        /// </summary>
        private IReadOnlyDictionary<string, ProjectRef>? _projectOfEveryFile;

        public MsBuildSolutionExplorer(
            Workspace workspace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            _workspace = workspace;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> GetAllFilePathsAsync()
        {
            return Task.FromResult<IReadOnlyList<string>>(
                ProjectOfEveryFile().Keys.ToList()
                );
        }

        /// <inheritdoc/>
        public Task<IReadOnlyDictionary<string, ProjectRef>> GetProjectOfEveryFileAsync()
        {
            return Task.FromResult(ProjectOfEveryFile());
        }

        private IReadOnlyDictionary<string, ProjectRef> ProjectOfEveryFile()
        {
            if (_projectOfEveryFile != null)
            {
                return _projectOfEveryFile;
            }

            var result = new Dictionary<string, ProjectRef>(StringComparer.OrdinalIgnoreCase);

            //a file which several projects compile (a shared project, a target framework of a
            //multi target project) is reported with the first of them, as the interface promises
            foreach (var project in ProjectsWithFile())
            {
                var projectRef = new ProjectRef(NameOf(project), project.FilePath!);
                var projectFolder = Path.GetDirectoryName(project.FilePath!);

                foreach (var document in project.Documents)
                {
                    AddSourceFile(result, document.FilePath, projectFolder, projectRef);
                }

                foreach (var document in project.AdditionalDocuments)
                {
                    AddSourceFile(result, document.FilePath, projectFolder, projectRef);
                }
            }

            //the deepest project first: a project inside the folder of another one owns its files
            foreach (var project in ProjectsWithFile().OrderByDescending(p => p.FilePath!.Length))
            {
                var projectFolder = Path.GetDirectoryName(project.FilePath!);
                if (string.IsNullOrEmpty(projectFolder))
                {
                    continue;
                }

                var projectRef = new ProjectRef(NameOf(project), project.FilePath!);

                foreach (var xamlFilePath in EnumerateXamlFiles(projectFolder!))
                {
                    Add(result, xamlFilePath, projectRef);
                }
            }

            _projectOfEveryFile = result;

            return result;
        }

        /// <summary>
        /// The projects of the workspace we are able to say anything about. A project without
        /// a file on the disk (Roslyn allows such a one) has no folder and therefore no
        /// namespace rule.
        /// </summary>
        private IEnumerable<Project> ProjectsWithFile()
        {
            return _workspace.CurrentSolution.Projects.Where(p => !string.IsNullOrEmpty(p.FilePath));
        }

        /// <summary>
        /// A document of a project, unless it is a generated one: the assembly info and the
        /// output of the source generators lie under <c>obj</c> and are recreated by every
        /// build, there is nothing to adjust in them.
        /// </summary>
        private static void AddSourceFile(
            Dictionary<string, ProjectRef> result,
            string? filePath,
            string? projectFolder,
            ProjectRef projectRef
            )
        {
            if (!string.IsNullOrEmpty(filePath) && IsBuildOutput(filePath!, projectFolder))
            {
                return;
            }

            Add(result, filePath, projectRef);
        }

        private static void Add(
            Dictionary<string, ProjectRef> result,
            string? filePath,
            ProjectRef projectRef
            )
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            if (result.ContainsKey(filePath!))
            {
                return;
            }

            result[filePath!] = projectRef;
        }

        private static IEnumerable<string> EnumerateXamlFiles(
            string projectFolder
            )
        {
            if (!Directory.Exists(projectFolder))
            {
                return Enumerable.Empty<string>();
            }

            return AdjustNamespace.Xaml.XamlPathHelper
                .EnumerateXamlFiles(projectFolder)
                .Where(filePath => !IsBuildOutput(filePath, projectFolder))
                ;
        }

        private static bool IsBuildOutput(
            string filePath,
            string? projectFolder
            )
        {
            //a file linked into the project from the outside is not an output of its build.
            //the comparison stops at the folder border: otherwise a sibling folder whose
            //name merely begins with the project folder (`MyApp.Tests`) would look like
            //it belongs here, and a linked file under its `obj` would be skipped.
            if (string.IsNullOrEmpty(projectFolder)
                || !IsSameFolderOrBelow(filePath, projectFolder!))
            {
                return false;
            }

            var relativePath = filePath.Substring(projectFolder!.Length)
                .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var folders = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return folders.Any(
                folder =>
                    string.Equals(folder, "bin", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(folder, "obj", StringComparison.OrdinalIgnoreCase)
                );
        }

        /// <summary>
        /// The path is the given folder or lies inside it. Stops at the folder border,
        /// see <see cref="Namespace.TargetNamespaceCalculator"/>.
        /// </summary>
        private static bool IsSameFolderOrBelow(
            string path,
            string rootFolder
            )
        {
            if (path.Length < rootFolder.Length)
            {
                return false;
            }

            if (!path.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (path.Length == rootFolder.Length)
            {
                return true;
            }

            if (IsSeparator(rootFolder[rootFolder.Length - 1]))
            {
                return true;
            }

            return IsSeparator(path[rootFolder.Length]);
        }

        private static bool IsSeparator(char c)
        {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }

        /// <summary>
        /// Name of the project without the target framework Roslyn adds to it: a multi target
        /// project is a separate Roslyn project per framework (<c>MyApp (net8.0)</c>), while
        /// the rest of the code knows the single project of the solution.
        /// </summary>
        private static string NameOf(
            Project project
            )
        {
            var name = project.Name;

            var frameworkStart = name.IndexOf(" (", StringComparison.Ordinal);

            return frameworkStart > 0
                ? name.Substring(0, frameworkStart)
                : name
                ;
        }
    }
}
