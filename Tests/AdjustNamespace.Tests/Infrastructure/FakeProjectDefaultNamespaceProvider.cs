using AdjustNamespace.Namespace;
using AdjustNamespace.VisualStudio;
using System.Threading.Tasks;

namespace AdjustNamespace.Tests.Infrastructure
{
    /// <summary>
    /// The default namespace of a project without Visual Studio behind it.
    ///
    /// The real implementation reads the <c>DefaultNamespace</c> property of the project
    /// through DTE and falls back to the name of the project without its last part; there are
    /// no project properties here, so the fallback is the only answer — which is exactly what
    /// Visual Studio does for a project of a kind it knows nothing about.
    /// </summary>
    public sealed class FakeProjectDefaultNamespaceProvider : IProjectDefaultNamespaceProvider
    {
        /// <inheritdoc/>
        public Task<string> GetAsync(ProjectRef project, string documentFilePath)
        {
            return Task.FromResult(
                TargetNamespaceCalculator.DefaultNamespaceFallback(project.Name)
                );
        }
    }
}
