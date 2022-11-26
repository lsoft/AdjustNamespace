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
using System.Linq;
using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;

namespace AdjustNamespace
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class AdjustSolutionCommand
    {
        public static string ProjectKind = "{52AEFF70-BBD8-11d2-8598-006097C68E81}";


        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0301;

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
        private AdjustSolutionCommand(AsyncPackage package, OleMenuCommandService commandService)
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
        public static AdjustSolutionCommand? Instance
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
            Instance = new AdjustSolutionCommand(package, commandService!);
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

                var vss = await VsServices.CreateAsync(ServiceProvider);
                filePaths.AddRange(vss.Dte.Solution.ProcessSolution());

                if (filePaths.Count > 0)
                {
                    var window = AdjustNamespaceWindow.Create(vss, filePaths);
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
