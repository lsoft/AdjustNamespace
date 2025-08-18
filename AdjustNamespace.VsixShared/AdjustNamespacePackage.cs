global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using AdjustNamespace.Command;
using AdjustNamespace.Helper;
using AdjustNamespace.InfoBar;
using AdjustNamespace.Options;
using EnvDTE80;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Threading;

namespace AdjustNamespace
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidAdjustNamespacePackageString)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Adjust Namespaces", "General", 0, 0, true, SupportsProfiles = true)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExistsAndFullyLoaded_string, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class AdjustNamespacePackage : ToolkitPackage
    {
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