using System;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// A file of the solution together with the project it belongs to.
    /// </summary>
    public readonly struct ProjectItemInformation
    {
        /// <summary>
        /// Project the file belongs to.
        /// </summary>
        public readonly SolutionItem Project;

        /// <summary>
        /// Project item of the file.
        /// </summary>
        public readonly SolutionItem ProjectItem;

        public ProjectItemInformation(
            SolutionItem project,
            SolutionItem projectItem
            )
        {
            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (projectItem is null)
            {
                throw new ArgumentNullException(nameof(projectItem));
            }

            Project = project;
            ProjectItem = projectItem;
        }
    }
}
