using EnvDTE;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Helper
{
    public static class SolutionHelper
    {
        public static string ProjectItemKindFolder = "{6BB5F8EF-4483-11D3-8BCF-00C04F8EC28C}";
        public static string ProjectItemKindFile = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";


        public static ProjectItem? GetProjectItem(
            this Solution solution,
            string filePath
            )
        {
            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            var prjItems = new List<ProjectItem>();
            if (solution.Projects != null)
            {
                foreach (EnvDTE.Project project in solution.Projects)
                {
                    if (project.ProjectItems != null && project.ProjectItems.Count > 0)
                    {
                        foreach (ProjectItem projectItem in project.ProjectItems)
                        {
                            prjItems.Clear();
                            ProcessProjectItem2(projectItem, ref prjItems);

                            foreach (var prjItem in prjItems)
                            {
                                for (var i = 0; i < prjItem.FileCount; i++)
                                {
                                    var itemPath = prjItem.FileNames[(short)i];
                                    if (itemPath == filePath)
                                    {
                                        return prjItem;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static void ProcessProjectItem2(
            ProjectItem projectItem,
            ref List<ProjectItem> result
            )
        {
            if (projectItem is null)
            {
                throw new ArgumentNullException(nameof(projectItem));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            result.Add(projectItem);

            if (projectItem.ProjectItems != null && projectItem.ProjectItems.Count > 0)
            {
                foreach (ProjectItem spi in projectItem.ProjectItems)
                {
                    ProcessProjectItem2(spi, ref result);
                }
            }
        }

        public static List<string> ProcessSolution(
            this EnvDTE.Solution solution
            )
        {
            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            var filePaths = new List<string>();

            foreach (EnvDTE.Project prj in solution.Projects)
            {
                filePaths.AddRange(prj.ProcessProject());
            }

            return filePaths;
        }

        public static List<string> ProcessProject(
            this EnvDTE.Project project
            )
        {
            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            var filePaths = new List<string>();

            if (project.ProjectItems != null && project.ProjectItems.Count > 0)
            {
                foreach (ProjectItem prjItem in project.ProjectItems)
                {
                    filePaths.AddRange(prjItem.ProcessProjectItem());
                }
            }

            return filePaths;
        }

        public static List<string> ProcessProjectItem(
            this ProjectItem projectItem
            )
        {
            if (projectItem is null)
            {
                throw new ArgumentNullException(nameof(projectItem));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            var result = new List<string>();

            if (projectItem.Kind.NotIn(ProjectItemKindFolder, ProjectItemKindFile))
            {
                return result;
            }

            for (var i = 0; i < projectItem.FileCount; i++)
            {
                var itemPath = projectItem.FileNames[(short)i];

                if (projectItem.Kind == ProjectItemKindFile)
                {
                    result.Add(itemPath);
                }

                if (projectItem.ProjectItems != null && projectItem.ProjectItems.Count > 0)
                {
                    foreach (ProjectItem spi in projectItem.ProjectItems)
                    {
                        result.AddRange(ProcessProjectItem(spi));
                    }
                }
            }

            return result;
        }

    }
}
