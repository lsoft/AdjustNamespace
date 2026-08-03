using AdjustNamespace.Settings;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using System.IO;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    /// <summary>
    /// The set of the Visual Studio services the extension works with,
    /// plus the settings of the currently opened solution.
    /// It is created once per command invocation and is passed everywhere by value.
    /// </summary>
    public readonly struct VsServices
    {
        /// <summary>
        /// Service provider of the package.
        /// </summary>
        public readonly IAsyncServiceProvider ServiceProvider;

        /// <summary>
        /// DTE, the automation object model of Visual Studio.
        /// </summary>
        public readonly DTE2 Dte;

        /// <summary>
        /// MEF composition container of Visual Studio.
        /// </summary>
        public readonly IComponentModel ComponentModel;

        /// <summary>
        /// Roslyn workspace of the currently opened solution.
        /// It is a <see cref="VisualStudioWorkspace"/> in the real life; the base type is
        /// declared here because nothing in the core needs more than the base API, and this
        /// allows to run the core over an <c>AdhocWorkspace</c> in the tests
        /// (see <see cref="CreateForTests"/>).
        /// </summary>
        public readonly Microsoft.CodeAnalysis.Workspace Workspace;

        /// <summary>
        /// Reader/writer of the settings file which lives in the solution folder.
        /// </summary>
        public readonly SettingsReader SettingsReader;

        /// <summary>
        /// Settings of the currently opened solution.
        /// </summary>
        public readonly AdjustNamespaceSettings2 Settings;

        /// <summary>
        /// Build a services set over an arbitrary Roslyn workspace, without any Visual Studio
        /// service behind it. For the tests of the core only: everything which touches DTE,
        /// the component model or the editor will fail with a <see cref="NullReferenceException"/>.
        /// </summary>
        /// <param name="workspace">Roslyn workspace to work with (an <c>AdhocWorkspace</c>, usually).</param>
        /// <param name="settings">Settings of the "solution" of that workspace.</param>
        public static VsServices CreateForTests(
            Microsoft.CodeAnalysis.Workspace workspace,
            AdjustNamespaceSettings2 settings
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (settings is null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return new VsServices(workspace, settings);
        }

        private VsServices(
            Microsoft.CodeAnalysis.Workspace workspace,
            AdjustNamespaceSettings2 settings
            )
        {
            ServiceProvider = null!;
            Dte = null!;
            ComponentModel = null!;
            Workspace = workspace;
            SettingsReader = null!;
            Settings = settings;
        }

        private VsServices(
            Microsoft.VisualStudio.Shell.IAsyncServiceProvider serviceProvider,
            DTE2 dte,
            IComponentModel componentModel,
            VisualStudioWorkspace workspace
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (dte is null)
            {
                throw new ArgumentNullException(nameof(dte));
            }

            if (componentModel is null)
            {
                throw new ArgumentNullException(nameof(componentModel));
            }

            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            ServiceProvider = serviceProvider;
            Dte = dte;
            ComponentModel = componentModel;
            Workspace = workspace;

            var solutionFolder = new FileInfo(workspace.CurrentSolution.FilePath).Directory.FullName;

            SettingsReader = new SettingsReader(solutionFolder);
            Settings = new AdjustNamespaceSettings2(
                solutionFolder,
                SettingsReader.ReadSettings() ?? new AdjustNamespaceSettings()
                );
        }

        /// <summary>
        /// Open the given file in the Visual Studio editor.
        /// </summary>
        public async Task OpenFileAsync(
            string documentFullPath
            )
        {
            _ = await VS.Documents.OpenAsync(documentFullPath);
        }

        /// <summary>
        /// Resolve all the required Visual Studio services and read the solution settings.
        /// </summary>
        /// <exception cref="InvalidOperationException">One of the services is not available.</exception>
        public static async Task<VsServices> CreateAsync(
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

            var componentModel = (IComponentModel)(await serviceProvider.GetServiceAsync(typeof(SComponentModel)))!;
            if (componentModel == null)
            {
                throw new InvalidOperationException("Can't create a component model");
            }

            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null)
            {
                throw new InvalidOperationException("Can't create a workspace");
            }

            return new VsServices(serviceProvider, dte, componentModel, workspace);
        }
    }
}
