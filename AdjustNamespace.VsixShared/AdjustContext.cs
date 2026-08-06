using AdjustNamespace.Namespace;
using AdjustNamespace.Settings;
using AdjustNamespace.VisualStudio;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.LanguageServices;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    /// <summary>
    /// Everything one adjusting session works with.
    ///
    /// It replaces the former <c>VsServices</c>, which carried the raw Visual Studio services
    /// (DTE, the component model, the service provider) and had to be built with four <c>null!</c>
    /// fields for the tests: the boundary between the core and the IDE was not expressed in the
    /// types, so a test which accidentally touched the IDE failed with a
    /// <see cref="NullReferenceException"/> instead of a compilation error.
    ///
    /// Every member here is a real one in the tests as well: the Roslyn workspace is an
    /// <c>AdhocWorkspace</c> and the two Visual Studio bound members are interfaces with a fake
    /// behind them. The raw services are gone — the component model and the service provider
    /// were used nowhere, and DTE is resolved by the two places which really need it
    /// (<see cref="DteProjectDefaultNamespaceProvider"/> and the solution explorer command).
    /// </summary>
    public sealed class AdjustContext
    {
        /// <summary>
        /// Roslyn workspace of the currently opened solution.
        /// It is a <see cref="VisualStudioWorkspace"/> in the real life; the base type is
        /// declared here because nothing in the core needs more than the base API, and this
        /// allows to run the core over an <c>AdhocWorkspace</c> in the tests.
        /// </summary>
        public Microsoft.CodeAnalysis.Workspace Workspace
        {
            get;
        }

        /// <summary>
        /// The solution tree: the files of the solution and their projects.
        /// </summary>
        public ISolutionExplorer Solution
        {
            get;
        }

        /// <summary>
        /// The namespace a file has to be moved into.
        /// </summary>
        public TargetNamespaceResolver TargetNamespaces
        {
            get;
        }

        /// <summary>
        /// Opener of the changed files in the editor, which is what makes the changes undoable.
        /// It is asked only if the user has switched that option on.
        /// </summary>
        public IDocumentOpener DocumentOpener
        {
            get;
        }

        public AdjustContext(
            Microsoft.CodeAnalysis.Workspace workspace,
            ISolutionExplorer solution,
            TargetNamespaceResolver targetNamespaces,
            IDocumentOpener documentOpener
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            if (targetNamespaces is null)
            {
                throw new ArgumentNullException(nameof(targetNamespaces));
            }

            if (documentOpener is null)
            {
                throw new ArgumentNullException(nameof(documentOpener));
            }

            Workspace = workspace;
            Solution = solution;
            TargetNamespaces = targetNamespaces;
            DocumentOpener = documentOpener;
        }

        /// <summary>
        /// Resolve all the required Visual Studio services and read the solution settings.
        /// </summary>
        /// <exception cref="InvalidOperationException">One of the services is not available.</exception>
        public static async Task<AdjustContext> CreateAsync(
            Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            var dte = await serviceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
            if (dte == null)
            {
                throw new InvalidOperationException("Can't create a dte");
            }

            var componentModel = (Microsoft.VisualStudio.ComponentModelHost.IComponentModel)
                (await serviceProvider.GetServiceAsync(typeof(Microsoft.VisualStudio.ComponentModelHost.SComponentModel)))!;
            if (componentModel == null)
            {
                throw new InvalidOperationException("Can't create a component model");
            }

            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null)
            {
                throw new InvalidOperationException("Can't create a workspace");
            }

            var settings = ReadSettingsOf(workspace);

            return new AdjustContext(
                workspace,
                new VsSolutionExplorer(),
                new TargetNamespaceResolver(
                    settings,
                    new DteProjectDefaultNamespaceProvider(dte)
                    ),
                new VsDocumentOpener()
                );
        }

        /// <summary>
        /// Read the settings file which lives in the folder of the given solution.
        /// </summary>
        public static AdjustNamespaceSettings2 ReadSettingsOf(
            Microsoft.CodeAnalysis.Workspace workspace
            )
        {
            var solutionFolder = new FileInfo(workspace.CurrentSolution.FilePath).Directory.FullName;

            return ReadSettingsOf(solutionFolder);
        }

        /// <summary>
        /// Read the settings file which lives in the given solution folder.
        /// </summary>
        public static AdjustNamespaceSettings2 ReadSettingsOf(
            string solutionFolder
            )
        {
            var reader = new SettingsReader(solutionFolder);

            return new AdjustNamespaceSettings2(
                solutionFolder,
                reader.ReadSettings() ?? new AdjustNamespaceSettings()
                );
        }
    }
}
