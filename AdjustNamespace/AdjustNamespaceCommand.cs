using AdjustNamespace.Helper;
using AdjustNamespace.Window;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;
using AdjustNamespace.UI.StepFactory;

namespace AdjustNamespace
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class AdjustNamespaceCommand
    {
        public static string ProjectKind = "{52AEFF70-BBD8-11d2-8598-006097C68E81}";


        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

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
        public static async Task InitializeAsync(AsyncPackage package)
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
                var filePaths = new List<string>();

                var dte = await ServiceProvider.GetServiceAsync(typeof(DTE)) as DTE2;
                if (dte == null)
                {
                    return;
                }

                var componentModel = (IComponentModel)await ServiceProvider.GetServiceAsync(typeof(SComponentModel));
                if (componentModel == null)
                {
                    return;
                }

                var workspace = componentModel.GetService<VisualStudioWorkspace>();
                if (workspace == null)
                {
                    return;
                }

                if (dte.ActiveWindow.Type == vsWindowType.vsWindowTypeSolutionExplorer)
                {
                    var uih = dte.ToolWindows.SolutionExplorer;
                    var selectedItems = (Array)uih.SelectedItems;

                    if (null != selectedItems)
                    {
                        foreach (UIHierarchyItem selItem in selectedItems)
                        {
                            if ((selItem.Object as dynamic).ExtenderCATID == ProjectKind)
                            {
                                filePaths.AddRange(dte.Solution.ProcessSolution());
                            }

                            if (selItem.Object is EnvDTE.Project project)
                            {
                                filePaths.AddRange(project.ProcessProject());
                            }

                            if (selItem.Object is ProjectItem projectItem)
                            {
                                filePaths.AddRange(projectItem.ProcessProjectItem());
                            }
                        }
                    }
                }

                if (filePaths.Count > 0)
                {

                    var window = new AdjustNamespaceWindow(
                        filePaths,
                        async anw =>
                        {
                            var perfsf = new PerformingStepFactory(
                                ServiceProvider,
                                anw,
                                anw.CenterContentControl
                                );

                            var prepsf = new PreparationStepFactory(
                                ServiceProvider,
                                anw.CenterContentControl,
                                perfsf
                                );

                            await prepsf.CreateAsync(filePaths);
                        }
                        );

                    //var pps = new PreparationPageSetter(
                    //    ServiceProvider,
                    //    window.Dispatcher,
                    //    window.CenterContentControl
                    //    );

                    //var psf = new PreparationStepFactory(
                    //    ServiceProvider,
                    //    window.Dispatcher
                    //    );

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
