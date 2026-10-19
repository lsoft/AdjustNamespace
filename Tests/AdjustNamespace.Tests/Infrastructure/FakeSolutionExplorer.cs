using AdjustNamespace.VisualStudio;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Tests.Infrastructure
{
    /// <summary>
    /// The solution tree of a <see cref="TestSolution"/>.
    ///
    /// There is no Visual Studio here, so the tree is rebuilt out of the Roslyn workspace:
    /// the documents of the projects plus the xaml files which have been written to the disk.
    /// This is enough for everything the extension asks the tree about, and it replaces the
    /// former <c>null!</c> field of <c>VsServices</c> — a test which walks the solution tree
    /// gets an answer now instead of a <see cref="System.NullReferenceException"/>.
    /// </summary>
    public sealed class FakeSolutionExplorer : ISolutionExplorer
    {
        private readonly TestSolution _solution;

        /// <summary>
        /// How many times the whole tree has been asked for. Walking the tree is the expensive
        /// part in the real Visual Studio, so a session is expected to do it once.
        /// </summary>
        public int GetProjectOfEveryFileCallCount
        {
            get;
            private set;
        }

        public FakeSolutionExplorer(TestSolution solution)
        {
            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            _solution = solution;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> GetAllFilePathsAsync()
        {
            var result = new List<string>(_solution.AllDocumentPaths());

            //the xaml files are real files inside the solution folder and are no documents
            //of the workspace, see TestSolution.AddXamlFile
            if (Directory.Exists(_solution.SolutionFolder))
            {
                result.AddRange(
                    AdjustNamespace.Xaml.XamlPathHelper.EnumerateXamlFiles(_solution.SolutionFolder)
                    );
            }

            return Task.FromResult<IReadOnlyList<string>>(result);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> GetTextFilePathsAsync()
        {
            //the text files are real files inside the solution folder (and so are the project
            //files a test has written), see TestSolution.AddTextFile
            var result = Directory.Exists(_solution.SolutionFolder)
                ? Directory.GetFiles(_solution.SolutionFolder, "*", SearchOption.AllDirectories).ToList()
                : new List<string>();

            return Task.FromResult<IReadOnlyList<string>>(result);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyDictionary<string, ProjectRef>> GetProjectOfEveryFileAsync()
        {
            GetProjectOfEveryFileCallCount++;

            return Task.FromResult<IReadOnlyDictionary<string, ProjectRef>>(
                _solution.ProjectOfEveryFile()
                );
        }
    }
}
