global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using AdjustNamespace.Command;
using AdjustNamespace.UI;
using AdjustNamespace.InfoBar;
using AdjustNamespace.Options;
using EnvDTE80;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Threading;

namespace AdjustNamespace
{
    /// <summary>
    /// The VSIX package of the extension.
    /// It is loaded in the background (with and without a solution opened) and
    /// only registers the commands; all the real work is started from the commands
    /// (see the <c>AdjustNamespace.Command</c> namespace).
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidAdjustNamespacePackageString)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Adjust Namespaces", "General", 0, 0, true, SupportsProfiles = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class AdjustNamespacePackage : ToolkitPackage
    {
        /// <summary>
        /// Package entry point. Registers every menu command of the extension,
        /// shows the release notes gold bar (if the extension has been updated)
        /// and loads the shared WPF resources used by the wizard.
        /// </summary>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            
            await AdjustNamespaceCommand.InitializeAsync(this);
            await AdjustSolutionCommand.InitializeAsync(this);
            await AdjustSelectedCommand.InitializeAsync(this);
            await EditSkippedPathsCommand.InitializeAsync(this);
            await ShowReleaseNotesCommand.InitializeAsync(this);

            await this.RegisterCommandsAsync();

            ShowReleaseNotesInfoBarIfNeeded();

            EmbeddedResourceHelper.LoadXamlEmbeddedResource(
                "AdjustNamespace.UI.TextLikeButtonResource.xaml"
                );
        }

        /// <summary>
        /// Show the gold bar with a link to the release notes if the currently installed
        /// version differs from the version the user has seen last time.
        /// </summary>
        private static void ShowReleaseNotesInfoBarIfNeeded()
        {
            if (Vsix.Version != General.Instance.LastVersion)
            {
                var dte = AsyncPackage.GetGlobalService(typeof(EnvDTE.DTE)) as DTE2;
                var sp = new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)dte!);
                ReleaseNotesInfoBarService.Initialize(sp);
                ReleaseNotesInfoBarService.Instance.ShowInfoBar();
            }
        }

    }
}