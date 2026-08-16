using AdjustNamespace.VisualStudio;
using AdjustNamespace.Window;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Linq;

namespace AdjustNamespace.Command
{
    /// <summary>
    /// Handler of the `Adjust namespaces...` command from the solution explorer context menu.
    /// Collects the files of the selected solution explorer items and opens the wizard
    /// (<see cref="AdjustNamespaceWindow"/>) for them.
    /// </summary>
    internal sealed class AdjustNamespaceCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = PackageIds.DoAdjustCommandId;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("3f7538ed-5c20-4d49-89fc-c401bb76df25");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        /// <summary>
        /// Initializes a new instance of the <see cref="AdjustNamespaceCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private AdjustNamespaceCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static AdjustNamespaceCommand? Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async System.Threading.Tasks.Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new AdjustNamespaceCommand(package, commandService!);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private async void Execute(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            try
            {
                //HashSet is needed to remove duplicates paths
                //this is possible if you click Adjust on xaml file (with cs behind)
                var filePaths = new HashSet<string>();

                var context = await VsAdjustContext.CreateAsync(ServiceProvider);

                var dte = await ServiceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;

                //this command is bound to the solution explorer context menu only,
                //so there is nothing to do if the command has been invoked from elsewhere
                if (dte != null && dte.ActiveWindow.Type == vsWindowType.vsWindowTypeSolutionExplorer)
                {
                    var sew = await VS.Windows.GetSolutionExplorerWindowAsync();
                    if (sew != null)
                    {
                        var selection = await sew.GetSelectionAsync();

                        //the selected item may be a solution, a project, a folder or a file,
                        //so we need to descend to the physical files in any case
                        foreach (var item in selection)
                        {
                            var files = await item.ProcessDownRecursivelyForAsync(SolutionItemType.PhysicalFile, null);
                            filePaths.AddRange(
                                files.ConvertAll(i => i.FullPath!).FindAll(i => !string.IsNullOrEmpty(i))
                                );
                        }
                    }
                }

                if (filePaths.Count > 0)
                {
                    var window = AdjustNamespaceWindow.Create(context, filePaths);
                    window.ShowModal();
                }
            }
            catch (Exception excp)
            {
                Logging.LogVS(excp);
            }
        }


        //private void ShowError(string errorMessage)
        //{
        //    VsShellUtilities.ShowMessageBox(
        //        package,
        //        errorMessage,
        //        $"Error has been found",
        //        OLEMSGICON.OLEMSGICON_WARNING,
        //        OLEMSGBUTTON.OLEMSGBUTTON_OK,
        //        OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST
        //        );
        //}
    }
}
