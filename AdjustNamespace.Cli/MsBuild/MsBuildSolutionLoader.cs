using Microsoft.CodeAnalysis.MSBuild;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Cli.MsBuild
{
    /// <summary>
    /// Opens a solution or a single project into a Roslyn workspace.
    ///
    /// This is what Visual Studio does for the extension: everything below the workspace works
    /// with the loaded solution and does not care where it came from. A <c>.slnx</c> is parsed
    /// by <c>Microsoft.CodeAnalysis.Workspaces.MSBuild</c> itself since its version 5.0.
    /// </summary>
    public static class MsBuildSolutionLoader
    {
        /// <summary>
        /// Open the given solution (<c>.sln</c>, <c>.slnx</c>) or project (<c>.csproj</c>).
        /// The caller owns the returned workspace.
        /// </summary>
        /// <param name="solutionPath">Full path to the solution or to the project.</param>
        /// <param name="loadFailures">
        /// (out) What MSBuild has complained about while loading. A failure here is not fatal
        /// — a project which has not been loaded is simply not adjusted — but the user has to
        /// know about it.
        /// </param>
        /// <param name="cancellationToken">Cancellation of the run.</param>
        public static async Task<MSBuildWorkspace> OpenAsync(
            string solutionPath,
            List<string> loadFailures,
            CancellationToken cancellationToken
            )
        {
            if (solutionPath is null)
            {
                throw new ArgumentNullException(nameof(solutionPath));
            }

            if (loadFailures is null)
            {
                throw new ArgumentNullException(nameof(loadFailures));
            }

            var workspace = MSBuildWorkspace.Create();

            //the subscription lives as long as the workspace does
            _ = workspace.RegisterWorkspaceFailedHandler(e => loadFailures.Add(e.Diagnostic.Message));

            try
            {
                if (Path.GetExtension(solutionPath).ToLowerInvariant() == ".csproj")
                {
                    await workspace.OpenProjectAsync(
                        solutionPath,
                        cancellationToken: cancellationToken
                        );
                }
                else
                {
                    await workspace.OpenSolutionAsync(
                        solutionPath,
                        cancellationToken: cancellationToken
                        );
                }
            }
            catch
            {
                workspace.Dispose();
                throw;
            }

            return workspace;
        }
    }
}
