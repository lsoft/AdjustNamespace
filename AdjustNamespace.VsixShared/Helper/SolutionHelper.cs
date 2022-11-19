using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;

namespace AdjustNamespace.Helper
{
    public static class SolutionHelper
    {
        public static string ProjectItemKindFolder = "{6BB5F8EF-4483-11D3-8BCF-00C04F8EC28C}";
        public static string ProjectItemKindFile = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";

        public static string DatabaseProjectItemKindFolder = "{6bb5f8ef-4483-11d3-8bcf-00c04f8ec28c}";
        public static string DatabaseProjectItemKindFile = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";

        public static bool TryGetProjectItem(
            this Solution solution,
            string filePath,
            out Project? rProject,
            out ProjectItem? rProjectItem
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

            var projectList = solution.GetAllProjectsRecursively();

            var prjItems = new List<ProjectItem>();
            foreach (EnvDTE.Project project in projectList)
            {
                if (project.ProjectItems == null || project.ProjectItems.Count <= 0)
                {
                    continue;
                }

                foreach (ProjectItem projectItem in project.ProjectItems)
                {
                    prjItems.Clear();
                    ProcessProjectItem2(projectItem, ref prjItems);

                    foreach (var prjItem in prjItems)
                    {
                        for (var i = 0; i < prjItem.FileCount; i++)
                        {
                            if (prjItem.TryGetFileName(i, out var itemPath))
                            {
                                if (itemPath == filePath)
                                {
                                    rProject = project;
                                    rProjectItem = prjItem;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            rProject = null;
            rProjectItem = null;
            return false;
        }

        private static void GetSolutionFolderProjects(
            this Project project,
            ref List<Project> foundTrueProjects
            )
        {
            if (project == null)
            {
                return;
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            if (project.Kind != EnvDTE80.ProjectKinds.vsProjectKindSolutionFolder)
            {
                foundTrueProjects.Add(project);
                return;
            }

            for (var i = 1; i <= project.ProjectItems.Count; i++)
            {
                var subProject = project.ProjectItems.Item(i);
                if (subProject == null)
                {
                    continue;
                }
                if (subProject.SubProject == null)
                {
                    continue;
                }

                if (subProject.Kind != EnvDTE80.ProjectKinds.vsProjectKindSolutionFolder
                    && ((dynamic)subProject.Object).Kind == EnvDTE80.ProjectKinds.vsProjectKindSolutionFolder)
                {
                    subProject.SubProject.GetSolutionFolderProjects(ref foundTrueProjects);
                }
                else
                {
                    foundTrueProjects.Add(subProject.SubProject);
                }
            }
        }

        public static bool TryGetFileName(
            this ProjectItem prjItem,
            int index,
            out string? fileName
            )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                fileName = prjItem.FileNames[(short)index];
                return true;
            }
            catch (ArgumentException)
            {
                fileName = null;
                return false;
            }
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

        public static List<Project> GetAllProjectsRecursively(
            this EnvDTE.Solution solution
            )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var projectList = new List<Project>();
            if (solution.Projects != null)
            {
                foreach (EnvDTE.Project project in solution.Projects)
                {
                    GetSolutionFolderProjects(project, ref projectList);
                }
            }

            return projectList;
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


            var projectList = solution.GetAllProjectsRecursively();

            var filePaths = new List<string>();
            foreach (EnvDTE.Project prj in projectList)
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

            if (projectItem.Kind.NotIn(ProjectItemKindFolder, ProjectItemKindFile, DatabaseProjectItemKindFolder, DatabaseProjectItemKindFile))
            {
                return result;
            }

            for (var i = 0; i < projectItem.FileCount; i++)
            {
                var itemPath = projectItem.FileNames[(short)i];

                if (projectItem.Kind.In(ProjectItemKindFile, DatabaseProjectItemKindFile))
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
