using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// The real solution tree of Visual Studio, see <see cref="SolutionHelper"/>.
    /// Every method needs the main thread.
    /// </summary>
    public sealed class VsSolutionExplorer : ISolutionExplorer
    {
        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> GetAllFilePathsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            return await SolutionHelper.GetAllFilesFromAsync();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyDictionary<string, ProjectRef>> GetProjectOfEveryFileAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var projectItems = await SolutionHelper.GetAllProjectItemsAsync(null);

            var result = new Dictionary<string, ProjectRef>(projectItems.Count);
            foreach (var pair in projectItems)
            {
                result[pair.Key] = ToProjectRef(pair.Value);
            }

            return result;
        }

        private static ProjectRef ToProjectRef(ProjectItemInformation pii)
        {
            return new ProjectRef(
                pii.Project.Name,
                pii.Project.FullPath!
                );
        }
    }
}
