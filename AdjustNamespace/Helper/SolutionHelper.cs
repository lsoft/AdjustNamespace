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

        public static List<string> ProcessSolution(
            this EnvDTE.Solution solution,
            VisualStudioWorkspace workspace
            )
        {
            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            var filePaths = new List<string>();

            foreach (EnvDTE.Project prj in solution.Projects)
            {
                filePaths.AddRange(prj.ProcessProject(workspace));
            }

            return filePaths;
        }

        public static List<string> ProcessProject(
            this EnvDTE.Project project,
            VisualStudioWorkspace workspace
            )
        {
            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            var filePaths = new List<string>();

            foreach (ProjectItem prjItem in project.ProjectItems)
            {
                filePaths.AddRange(prjItem.ProcessProjectItem(workspace));
            }

            return filePaths;
        }

        public static List<string> ProcessProjectItem(
            this ProjectItem projectItem,
            VisualStudioWorkspace workspace
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
                        result.AddRange(ProcessProjectItem(spi, workspace));
                    }
                }
            }

            return result;
        }

    }
}
