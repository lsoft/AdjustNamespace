using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// Source of the default (root) namespace of a project.
    ///
    /// This is the only part of the target namespace rule which needs Visual Studio;
    /// everything else is a computation over the paths, see
    /// <see cref="Namespace.TargetNamespaceCalculator"/>. It is separated to keep that
    /// computation free of the main thread and therefore testable.
    /// </summary>
    public interface IProjectDefaultNamespaceProvider
    {
        /// <summary>
        /// The default namespace of the project the given file belongs to.
        /// </summary>
        /// <param name="project">The project the file belongs to.</param>
        /// <param name="documentFilePath">Full path to the file.</param>
        Task<string> GetAsync(
            ProjectRef project,
            string documentFilePath
            );
    }
}
