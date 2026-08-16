using AdjustNamespace.Namespace;
using AdjustNamespace.VisualStudio;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Cli.MsBuild
{
    /// <summary>
    /// The default namespace of a project taken from the project file.
    ///
    /// Visual Studio answers this with the <c>DefaultNamespace</c> property of the project
    /// (see <c>DteProjectDefaultNamespaceProvider</c>); MSBuild evaluates the very same
    /// <c>RootNamespace</c> property and Roslyn carries it in <see cref="Project.DefaultNamespace"/>.
    /// A project which declares none (a shared project, for example) falls back to the same rule
    /// the extension uses: <c>MyApp.Shared</c> -> <c>MyApp</c>.
    /// </summary>
    public sealed class ProjectFileDefaultNamespaceProvider : IProjectDefaultNamespaceProvider
    {
        private readonly Workspace _workspace;

        public ProjectFileDefaultNamespaceProvider(
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
        public Task<string> GetAsync(
            ProjectRef project,
            string documentFilePath
            )
        {
            var defaultNamespace = _workspace.CurrentSolution.Projects
                .Where(p => string.Equals(p.FilePath, project.FilePath, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.DefaultNamespace)
                .FirstOrDefault(dn => !string.IsNullOrEmpty(dn))
                ;

            if (string.IsNullOrEmpty(defaultNamespace))
            {
                return Task.FromResult(
                    TargetNamespaceCalculator.DefaultNamespaceFallback(project.Name)
                    );
            }

            return Task.FromResult(defaultNamespace!);
        }
    }
}
