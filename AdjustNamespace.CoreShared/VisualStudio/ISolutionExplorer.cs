using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// The solution tree of Visual Studio: which files it contains and which project every one
    /// of them belongs to.
    ///
    /// This is not the same as the Roslyn workspace: the tree knows every physical file of the
    /// solution (a xaml, a resource, a file of a project Roslyn does not compile at all), while
    /// the workspace knows the documents of the compilations only.
    ///
    /// Every method here needs the main thread, see <see cref="VsSolutionExplorer"/>.
    /// </summary>
    public interface ISolutionExplorer
    {
        /// <summary>
        /// Full paths of every physical file of the currently opened solution.
        /// </summary>
        Task<IReadOnlyList<string>> GetAllFilePathsAsync();

        /// <summary>
        /// Full paths of the files that may name a type as a text and are no C# documents: the
        /// project files, the settings files, the configs, the Razor views, the json files.
        /// The caller picks the ones it knows by their extension (see
        /// <c>TextReferenceFiles</c>), so it is fine to report more than that.
        ///
        /// The solution tree of Visual Studio knows every file of every project; the console
        /// utility walks the folders of the projects (without <c>bin</c> and <c>obj</c>).
        /// </summary>
        Task<IReadOnlyList<string>> GetTextFilePathsAsync();

        /// <summary>
        /// The map `file full path -> its project` for the whole solution.
        /// The tree is walked once and the answers for all the files of a session are taken
        /// out of the result, see <c>AdjustPlanner</c>. If a file belongs to more than one
        /// project (a shared project, a multi target project), the first found project wins.
        /// </summary>
        Task<IReadOnlyDictionary<string, ProjectRef>> GetProjectOfEveryFileAsync();
    }
}
