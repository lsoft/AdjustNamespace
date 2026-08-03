using AdjustNamespace.Adjusting;
using System.Linq;

namespace AdjustNamespace.Tests.Infrastructure
{
    /// <summary>
    /// The steps the wizard performs over a solution, without the wizard itself:
    /// the adjusting of a file (<see cref="CsAdjuster"/>) and the final cleanup
    /// (<see cref="Cleanup"/>), see <c>PerformingViewModel</c>.
    /// </summary>
    public static class AdjustRunner
    {
        /// <summary>
        /// Adjust a single file of the solution.
        /// </summary>
        /// <param name="solution">The solution to work with.</param>
        /// <param name="projectName">Project the file belongs to.</param>
        /// <param name="relativeFilePath">Path of the file relative to the project folder.</param>
        /// <param name="targetNamespace">The namespace the types of that file have to be moved into.</param>
        /// <param name="xamlFilePaths">The xaml files which may reference the moved types.</param>
        /// <returns>
        /// The namespace center of that run: it knows which namespaces became empty
        /// and is required by <see cref="CleanupAsync"/>.
        /// </returns>
        public static async System.Threading.Tasks.Task<NamespaceCenter> AdjustAsync(
            TestSolution solution,
            string projectName,
            string relativeFilePath,
            string targetNamespace,
            params string[] xamlFilePaths
            )
        {
            var namespaceCenter = await NamespaceCenter.CreateForAsync(solution.Workspace);

            await AdjustAsync(solution, namespaceCenter, projectName, relativeFilePath, targetNamespace, xamlFilePaths);

            return namespaceCenter;
        }

        /// <summary>
        /// Adjust a file within an existing session, i.e. with the namespace center
        /// of the previously adjusted files. This is what the wizard does when the user
        /// has selected more than one file.
        /// </summary>
        public static async Task AdjustAsync(
            TestSolution solution,
            NamespaceCenter namespaceCenter,
            string projectName,
            string relativeFilePath,
            string targetNamespace,
            params string[] xamlFilePaths
            )
        {
            var adjuster = new CsAdjuster(
                solution.Services,
                false,
                namespaceCenter,
                solution.PathOf(projectName, relativeFilePath),
                targetNamespace,
                xamlFilePaths.ToList()
                );

            await adjuster.AdjustAsync();
        }

        /// <summary>
        /// A whole session over a single file: the adjusting and the cleanup after it.
        /// </summary>
        public static async Task AdjustAndCleanupAsync(
            TestSolution solution,
            string projectName,
            string relativeFilePath,
            string targetNamespace,
            params string[] xamlFilePaths
            )
        {
            var namespaceCenter = await AdjustAsync(
                solution,
                projectName,
                relativeFilePath,
                targetNamespace,
                xamlFilePaths
                );

            await CleanupAsync(solution, namespaceCenter);
        }

        /// <summary>
        /// The final stage of the wizard: remove the using clauses of the emptied namespaces
        /// from every document of the solution.
        /// </summary>
        public static async Task CleanupAsync(
            TestSolution solution,
            NamespaceCenter namespaceCenter
            )
        {
            var cleanup = new Cleanup(solution.Services, namespaceCenter);

            foreach (var documentFilePath in solution.AllDocumentPaths())
            {
                await cleanup.RemoveEmptyUsingStatementsForAsync(documentFilePath);
            }
        }

        /// <summary>
        /// How many times the substring occurs in the text.
        /// </summary>
        public static int CountOf(string text, string substring)
        {
            var result = 0;

            var index = text.IndexOf(substring, StringComparison.Ordinal);
            while (index >= 0)
            {
                result++;
                index = text.IndexOf(substring, index + substring.Length, StringComparison.Ordinal);
            }

            return result;
        }
    }
}
