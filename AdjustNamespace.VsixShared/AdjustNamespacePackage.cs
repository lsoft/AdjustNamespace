global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using AdjustNamespace.Command;
using System.Runtime.InteropServices;
using System.Threading;

namespace AdjustNamespace
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.guidAdjustNamespacePackageString)]
    [ProvideOptionPage(typeof(Options.OptionsProvider.GeneralOptions), "Adjust Namespaces", "General", 0, 0, true, SupportsProfiles = true)]
    public sealed class AdjustNamespacePackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            
            await AdjustNamespaceCommand.InitializeAsync(this);
            await AdjustSolutionCommand.InitializeAsync(this);
            await AdjustSelectedCommand.InitializeAsync(this);
            await EditSkippedPathsCommand.InitializeAsync(this);

            await this.RegisterCommandsAsync();
        }
    }
}