﻿using AdjustNamespace.Helper;
using AdjustNamespace.Mover;
using AdjustNamespace.Window;
using EnvDTE;
using EnvDTE80;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Document = Microsoft.CodeAnalysis.Document;
using Task = System.Threading.Tasks.Task;

namespace AdjustNamespace
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class AdjustNamespaceCommand
    {
        public static string ProjectKind = "{52AEFF70-BBD8-11d2-8598-006097C68E81}";
        public static string ProjectItemKindFolder = "{6BB5F8EF-4483-11D3-8BCF-00C04F8EC28C}";
        public static string ProjectItemKindFile = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";


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

                //var message = string.Format(CultureInfo.CurrentCulture, "Inside {0}.MenuItemCallback()", GetType().FullName) + Environment.NewLine;
                //var title = "AdjustNamespace";

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
                    //var ad = dte.ActiveDocument;
                    //var ae = dte.ActiveSolutionProjects.GetValue(0);

                    var uih = dte.ToolWindows.SolutionExplorer;
                    var selectedItems = (Array)uih.SelectedItems;

                    if (null != selectedItems)
                    {
                        foreach (UIHierarchyItem selItem in selectedItems)
                        {
                            if ((selItem.Object as dynamic).ExtenderCATID == ProjectKind)
                            {
                                foreach (EnvDTE.Project prj in dte.Solution.Projects)
                                {
                                    foreach (ProjectItem prjItem in prj.ProjectItems)
                                    {
                                        filePaths.AddRange(ProcessProjectItem(workspace, prjItem));
                                    }
                                }
                            }

                            if (selItem.Object is EnvDTE.Project project)
                            {
                                foreach (ProjectItem prjItem in project.ProjectItems)
                                {
                                    filePaths.AddRange(ProcessProjectItem(workspace, prjItem));
                                }
                            }

                            if (selItem.Object is ProjectItem projectItem)
                            {
                                filePaths.AddRange(ProcessProjectItem(workspace, projectItem));
                            }
                        }
                    }
                }


                var filteredFilePaths = new List<string>();


                foreach (var filePath in filePaths)
                {
                    var subjectDocument = workspace.GetDocument(filePath);
                    if (!subjectDocument.IsDocumentInScope())
                    {
                        continue;
                    }

                    var project = subjectDocument!.Project;
                    if (!project.IsProjectInScope())
                    {
                        continue;
                    }

                    var projectFolderPath = new FileInfo(project.FilePath).Directory.FullName;
                    var suffix = new FileInfo(filePath).Directory.FullName.Substring(projectFolderPath.Length);
                    var targetNamespace = project.DefaultNamespace +
                        suffix
                            .Replace(Path.DirectorySeparatorChar, '.')
                            .Replace(Path.AltDirectorySeparatorChar, '.')
                            ;


                    var subjectSemanticModel = await subjectDocument.GetSemanticModelAsync();
                    if (subjectSemanticModel == null)
                    {
                        continue;
                    }

                    var subjectSyntaxRoot = await subjectDocument.GetSyntaxRootAsync();
                    if (subjectSyntaxRoot == null)
                    {
                        continue;
                    }

                    var subjectNamespaces = subjectSyntaxRoot
                        .DescendantNodesAndSelf()
                        .OfType<NamespaceDeclarationSyntax>()
                        .ToList()
                        ;
                    if (subjectNamespaces.Count == 0)
                    {
                        continue;
                    }

                    filteredFilePaths.Add(filePath);
                }

                if (filteredFilePaths.Count > 0)
                {
                    var mover = new AdjustChainMover(
                        ServiceProvider,
                        filteredFilePaths
                        );

                    var window = new AdjustNamespaceWindow(mover);
                    window.ShowModal();
                }
            }
            catch (Exception excp)
            {
                Logging.LogVS(excp);
            }
        }

        private static List<string> ProcessProjectItem(
            VisualStudioWorkspace workspace,
            ProjectItem projectItem
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (projectItem is null)
            {
                throw new ArgumentNullException(nameof(projectItem));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            var result = new List<string>();

            for (var i = 0; i < projectItem.FileCount; i++)
            {
                var itemPath = projectItem.FileNames[(short)i];

                if (projectItem.Kind == ProjectItemKindFolder)
                {
                    //nothing to do
                }
                else if (projectItem.Kind == ProjectItemKindFile)
                {
                    result.Add(itemPath);
                }

                if (projectItem.ProjectItems != null && projectItem.ProjectItems.Count > 0)
                {
                    foreach (ProjectItem spi in projectItem.ProjectItems)
                    {
                        result.AddRange(ProcessProjectItem(workspace, spi));
                    }
                }
            }

            return result;
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
