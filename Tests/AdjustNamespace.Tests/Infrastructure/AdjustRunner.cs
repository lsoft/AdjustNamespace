using AdjustNamespace.Adjusting;
using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Namespace;
using AdjustNamespace.VisualStudio;
using System.Linq;

namespace AdjustNamespace.Tests.Infrastructure
{
    /// <summary>
    /// The steps of an adjusting session, driven one by one: the decision what to do with
    /// a file (<see cref="AdjustPlanner"/>), the adjusting of it (<see cref="CsAdjuster"/>)
    /// and the final cleanup (<see cref="Cleanup"/>).
    ///
    /// The target namespace is given by the test and is not derived from the folders of the
    /// file, so the part of the planner which needs the solution tree is not involved here;
    /// that part is covered by <c>TargetNamespaceCalculatorTests</c>. A session which decides
    /// everything by itself, exactly as the wizard starts it, is
    /// <see cref="AdjustNamespace.Adjusting.Session.AdjustSession"/> and is driven by
    /// <c>AdjustSessionTests</c>.
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
            var plan = await AdjustPlanner.TryPlanAsync(
                solution.Workspace,
                solution.PathOf(projectName, relativeFilePath),
                targetNamespace
                );
            if (!plan.HasValue)
            {
                //the file is no subject to change at all, and the wizard creates
                //no adjuster for it
                return;
            }

            var adjuster = new CsAdjuster(
                solution.Workspace,
                new NullDocumentOpener(),
                false,
                namespaceCenter,
                plan.Value,
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
            var cleanup = new Cleanup(solution.Workspace, namespaceCenter);

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
