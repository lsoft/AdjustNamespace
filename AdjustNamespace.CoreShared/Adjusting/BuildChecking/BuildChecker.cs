using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.BuildChecking
{
    /// <summary>
    /// The optional build check of a run: the compilation errors of the solution before the run
    /// and after it, so the user is told which errors the run has introduced (if any).
    ///
    /// It asks the compilations of the workspace (the very thing the adjusting relies on) and
    /// not the build of the IDE, so it needs no build, no output folder and no main thread.
    /// </summary>
    public static class BuildChecker
    {
        /// <summary>
        /// The compilation errors of the C# projects of the solution.
        /// </summary>
        /// <param name="workspace">The workspace of the solution.</param>
        /// <param name="mainTypeNames">
        /// The new <c>StartupObject</c> of the projects whose project file the run has changed,
        /// keyed by the path of the project file. The workspace has read the project file when the
        /// solution was opened and does not read it again, so its compilation would still look for
        /// the old class (CS1555) while the real build, which reads the file, is fine.
        /// </param>
        /// <param name="cancellationToken">Cancellation of the session.</param>
        public static async Task<IReadOnlyList<CompilationErrorInfo>> CaptureAsync(
            Workspace workspace,
            IReadOnlyDictionary<string, string>? mainTypeNames = null,
            CancellationToken cancellationToken = default
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            var result = new List<CompilationErrorInfo>();

            foreach (var original in workspace.CurrentSolution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!original.IsProjectInScope())
                {
                    continue;
                }

                var project = WithMainTypeName(original, mainTypeNames);

                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation == null)
                {
                    continue;
                }

                foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
                {
                    if (diagnostic.Severity != DiagnosticSeverity.Error)
                    {
                        continue;
                    }

                    var span = diagnostic.Location.GetLineSpan();
                    var hasPosition = diagnostic.Location.IsInSource;

                    result.Add(
                        new CompilationErrorInfo(
                            project.Name,
                            diagnostic.Id,
                            diagnostic.GetMessage(),
                            hasPosition ? span.Path : null,
                            hasPosition ? span.StartLinePosition.Line + 1 : 0,
                            hasPosition ? span.StartLinePosition.Character : 0
                            )
                        );
                }
            }

            return result;
        }

        /// <summary>
        /// The project as it compiles with the <c>StartupObject</c> the run has written into its
        /// project file, if it has.
        /// </summary>
        private static Microsoft.CodeAnalysis.Project WithMainTypeName(
            Microsoft.CodeAnalysis.Project project,
            IReadOnlyDictionary<string, string>? mainTypeNames
            )
        {
            if (mainTypeNames == null
                || string.IsNullOrEmpty(project.FilePath)
                || !(project.CompilationOptions is Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions options))
            {
                return project;
            }

            foreach (var pair in mainTypeNames)
            {
                if (string.Equals(pair.Key, project.FilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return project.WithCompilationOptions(options.WithMainTypeName(pair.Value));
                }
            }

            return project;
        }

        /// <summary>
        /// The names of the projects a run has affected: the ones that have a changed file in
        /// their folder, and every project that depends on one of them.
        /// </summary>
        /// <param name="workspace">The workspace of the solution.</param>
        /// <param name="touchedFilePaths">The files the run has changed.</param>
        public static IReadOnlyList<string> AffectedProjectNames(
            Workspace workspace,
            IEnumerable<string> touchedFilePaths
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (touchedFilePaths is null)
            {
                throw new ArgumentNullException(nameof(touchedFilePaths));
            }

            var solution = workspace.CurrentSolution;
            var projectIds = new HashSet<ProjectId>();

            foreach (var filePath in touchedFilePaths)
            {
                var documents = workspace.GetDocuments(filePath);

                if (documents.Count > 0)
                {
                    foreach (var document in documents)
                    {
                        projectIds.Add(document.Project.Id);
                    }

                    continue;
                }

                //a file that is no document (a config, a view): the project whose folder holds it
                foreach (var project in workspace.GetProjectsOfFolderOf(filePath))
                {
                    projectIds.Add(project.Id);
                }
            }

            var graph = solution.GetProjectDependencyGraph();

            foreach (var projectId in projectIds.ToList())
            {
                foreach (var dependent in graph.GetProjectsThatTransitivelyDependOnThisProject(projectId))
                {
                    projectIds.Add(dependent);
                }
            }

            return projectIds
                .Select(id => solution.GetProject(id))
                .Where(p => p != null && p.IsProjectInScope())
                .Select(p => p!.Name)
                .Distinct()
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Compare the errors of the given projects before and after the run.
        /// </summary>
        public static BuildCheckResult Compare(
            IReadOnlyList<string> projectNames,
            IReadOnlyList<CompilationErrorInfo> before,
            IReadOnlyList<CompilationErrorInfo> after
            )
        {
            var names = new HashSet<string>(projectNames, StringComparer.OrdinalIgnoreCase);

            var beforeInProjects = before.Where(e => names.Contains(e.ProjectName)).ToList();
            var afterInProjects = after.Where(e => names.Contains(e.ProjectName)).ToList();

            //the same error is the same error where it has moved to: the run moves the lines
            var remaining = new Dictionary<string, int>();
            foreach (var error in beforeInProjects)
            {
                remaining.TryGetValue(error.Key, out var count);
                remaining[error.Key] = count + 1;
            }

            var newErrors = new List<CompilationErrorInfo>();
            foreach (var error in afterInProjects)
            {
                if (remaining.TryGetValue(error.Key, out var count) && count > 0)
                {
                    remaining[error.Key] = count - 1;
                    continue;
                }

                newErrors.Add(error);
            }

            return new BuildCheckResult(projectNames, beforeInProjects.Count, afterInProjects.Count, newErrors);
        }
    }
}
