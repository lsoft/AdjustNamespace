using AdjustNamespace.Settings;
using AdjustNamespace.VisualStudio;
using System;
using System.Threading.Tasks;

namespace AdjustNamespace.Namespace
{
    /// <summary>
    /// The namespace a file has to be moved into: the rule of
    /// <see cref="TargetNamespaceCalculator"/> plus the one step of it which asks
    /// Visual Studio (the default namespace of the project).
    ///
    /// It used to be an extension method on the <c>SolutionItem</c> of the solution tree, which
    /// made the whole rule reachable from the main thread only. The Visual Studio part is
    /// behind <see cref="IProjectDefaultNamespaceProvider"/> now, so this class works over
    /// a plain <see cref="ProjectRef"/> and is testable with a fake provider.
    /// </summary>
    public sealed class TargetNamespaceResolver
    {
        private readonly AdjustNamespaceSettings2 _settings;
        private readonly IProjectDefaultNamespaceProvider _defaultNamespaces;

        /// <param name="settings">Settings of the solution, they know the excluded folders.</param>
        /// <param name="defaultNamespaces">Source of the default namespace of a project.</param>
        public TargetNamespaceResolver(
            AdjustNamespaceSettings2 settings,
            IProjectDefaultNamespaceProvider defaultNamespaces
            )
        {
            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (defaultNamespaces is null)
            {
                throw new ArgumentNullException(nameof(defaultNamespaces));
            }

            _settings = settings;
            _defaultNamespaces = defaultNamespaces;
        }

        /// <summary>
        /// Determine the namespace the given file should belong to.
        /// </summary>
        /// <param name="project">Project the file belongs to.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        /// <param name="documentFilePath">Full path to the file.</param>
        /// <returns>
        /// The target namespace, or <c>null</c> if the file is outside of its project folder
        /// (a linked file, for example).
        /// </returns>
        public async Task<string?> TryResolveAsync(
            ProjectRef project,
            NamespaceReplaceRegex replaceRegex,
            string documentFilePath
            )
        {
            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            if (documentFilePath is null)
            {
                throw new ArgumentNullException(nameof(documentFilePath));
            }

            var folderChain = TargetNamespaceCalculator.TryGetFolderChain(
                project.FilePath,
                documentFilePath,
                _settings
                );
            if (folderChain == null)
            {
                //the file lies outside of the folder of its project, there is no
                //target namespace for it
                return null;
            }

            //the only step which needs Visual Studio, and it is performed after the check
            //above: a file we are not going to touch costs no switch to the main thread
            var projectDefaultNamespace = await _defaultNamespaces.GetAsync(
                project,
                documentFilePath
                );

            return TargetNamespaceCalculator.Compose(
                projectDefaultNamespace,
                folderChain,
                replaceRegex
                );
        }
    }
}
