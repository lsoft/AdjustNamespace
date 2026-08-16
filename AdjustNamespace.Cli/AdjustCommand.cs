using AdjustNamespace;
using AdjustNamespace.Adjusting;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Cli.CommandLine;
using AdjustNamespace.Cli.MsBuild;
using AdjustNamespace.Namespace;
using AdjustNamespace.VisualStudio;
using AdjustNamespace.Xaml.BodyProvider;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Cli
{
    /// <summary>
    /// One run of the utility, i.e. the same three steps the wizard of the extension walks the
    /// user through: the solution is loaded and compiled, the files which are the subject to
    /// change are collected and the session adjusts them.
    /// </summary>
    public sealed class AdjustCommand
    {
        /// <summary>
        /// How many compilation errors are shown before the rest of them is counted only.
        /// </summary>
        private const int MaxReportedCompilationErrors = 10;

        private readonly CliOptions _options;
        private readonly TextWriter _output;
        private readonly TextWriter _error;

        public AdjustCommand(
            CliOptions options,
            TextWriter output,
            TextWriter error
            )
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (error is null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            _options = options;
            _output = output;
            _error = error;
        }

        /// <summary>
        /// Perform the run.
        /// </summary>
        public async Task<ExitCode> RunAsync(
            CancellationToken cancellationToken
            )
        {
            var solutionFolder = Path.GetDirectoryName(_options.SolutionPath)!;

            if (_options.Debug)
            {
                var logPath = Path.Combine(Path.GetTempPath(), "AdjustNamespace.cli.log");
                AdjustLog.EnableToFile(logPath);
                _output.WriteLine($"Debug log: {logPath}");
            }

            _output.WriteLine($"Loading {_options.SolutionPath}");

            var loadFailures = new List<string>();
            using var workspace = await MsBuildSolutionLoader.OpenAsync(
                _options.SolutionPath,
                loadFailures,
                cancellationToken
                );

            ReportLoadFailures(loadFailures);

            var projects = workspace.CurrentSolution.Projects.ToList();
            if (projects.Count == 0)
            {
                _error.WriteLine("No project has been loaded, there is nothing to adjust.");

                return ExitCode.Error;
            }

            _output.WriteLine($"{projects.Count} project(s) loaded");

            if (!await CompilesAsync(workspace, cancellationToken))
            {
                return ExitCode.Error;
            }

            var context = CreateContext(workspace, solutionFolder);

            var collect = await CollectAsync(context, solutionFolder, cancellationToken);
            if (collect is null)
            {
                return ExitCode.Error;
            }

            ReportBlocked(collect.Blocked, solutionFolder);

            if (collect.SubjectFilePaths.Count > 0)
            {
                ReportPlan(collect.SubjectFilePaths, solutionFolder);
            }
            else if (collect.Blocked.Count == 0)
            {
                _output.WriteLine("Every namespace is in accordance with the location of its file, nothing to do.");
            }

            if (_options.DryRun || _options.Check)
            {
                if (collect.Blocked.Count > 0)
                {
                    return ExitCode.Error;
                }

                if (collect.SubjectFilePaths.Count == 0)
                {
                    return ExitCode.Success;
                }

                return _options.Check
                    ? ExitCode.AdjustmentRequired
                    : ExitCode.Success
                    ;
            }

            if (collect.SubjectFilePaths.Count > 0)
            {
                var adjustExit = await AdjustAsync(
                    context,
                    collect.SubjectFilePaths,
                    solutionFolder,
                    cancellationToken
                    );

                if (adjustExit != ExitCode.Success)
                {
                    return adjustExit;
                }
            }

            return collect.Blocked.Count > 0
                ? ExitCode.Error
                : ExitCode.Success
                ;
        }

        /// <summary>
        /// Everything the session works with. This is the console counterpart of
        /// <c>VsAdjustContext</c>: the same core with the file system instead of the IDE
        /// behind every boundary interface.
        /// </summary>
        private static AdjustContext CreateContext(
            Workspace workspace,
            string solutionFolder
            )
        {
            var settings = AdjustContext.ReadSettingsOf(solutionFolder);

            return new AdjustContext(
                workspace,
                new MsBuildSolutionExplorer(workspace),
                new TargetNamespaceResolver(
                    settings,
                    new ProjectFileDefaultNamespaceProvider(workspace)
                    ),
                //there is no editor to open a file in, and therefore no undo:
                //the safety net of the console utility is the version control
                new NullDocumentOpener(),
                new ClosedXamlBodyProviderFactory()
                );
        }

        /// <summary>
        /// The files which are really the subject to change and the ones which cannot be
        /// adjusted, or <c>null</c> if the collecting itself has failed unexpectedly.
        /// </summary>
        private async Task<CollectResult?> CollectAsync(
            AdjustContext context,
            string solutionFolder,
            CancellationToken cancellationToken
            )
        {
            var filter = new PathFilter(_options.Paths);
            var namedProjectFiles = await FilesOfTheNamedProjectAsync(context);

            var candidates = (await context.Solution.GetAllFilePathsAsync())
                .Where(filePath => namedProjectFiles is null || namedProjectFiles.Contains(filePath))
                .Where(filter.Matches)
                .ToList();

            foreach (var unmatched in filter.UnmatchedRoots)
            {
                _error.WriteLine($"warning: {unmatched} contains no file of this solution.");
            }

            if (candidates.Count == 0)
            {
                return new CollectResult(
                    new List<string>(),
                    Array.Empty<AdjustBlock>()
                    );
            }

            _output.WriteLine($"Scanning {candidates.Count} file(s)");

            var collector = new SubjectFileCollector(
                context,
                new HashSet<string>(candidates),
                _options.ReplaceRegex
                );

            try
            {
                var results = await collector.AnalyzeAndCollectAsync(
                    (current, total, filePath) =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (_options.Verbose)
                        {
                            _output.WriteLine(
                                $"  [{current}/{total}] {RelativePath.Of(filePath, solutionFolder)}"
                                );
                        }
                    });

                return new CollectResult(
                    results.CollectedFiles.ConvertAll(f => f.FilePath),
                    results.Blocked
                    );
            }
            catch (FileProcessException exception)
            {
                _error.WriteLine(
                    $"error: {exception.Message} ({RelativePath.Of(exception.FilePath, solutionFolder)})"
                    );

                return null;
            }
        }

        private void ReportBlocked(
            IReadOnlyList<AdjustBlock> blocked,
            string solutionFolder
            )
        {
            foreach (var block in blocked)
            {
                _error.WriteLine(
                    $"error: {block.Message} ({RelativePath.Of(block.FilePath, solutionFolder)})"
                    );
            }
        }

        /// <summary>
        /// The adjustable files and the blocked ones of one collect step.
        /// </summary>
        private sealed class CollectResult
        {
            public IReadOnlyList<string> SubjectFilePaths
            {
                get;
            }

            public IReadOnlyList<AdjustBlock> Blocked
            {
                get;
            }

            public CollectResult(
                IReadOnlyList<string> subjectFilePaths,
                IReadOnlyList<AdjustBlock> blocked
                )
            {
                SubjectFilePaths = subjectFilePaths;
                Blocked = blocked;
            }
        }

        /// <summary>
        /// The files of the project the user has named, or <c>null</c> if a whole solution is
        /// being adjusted. Opening a project loads the projects it references as well: they are
        /// needed for the semantic model and their references to the moved types are fixed too,
        /// but their own namespaces are none of our business here.
        /// </summary>
        private async Task<HashSet<string>?> FilesOfTheNamedProjectAsync(
            AdjustContext context
            )
        {
            if (!string.Equals(
                    Path.GetExtension(_options.SolutionPath),
                    ".csproj",
                    StringComparison.OrdinalIgnoreCase
                    ))
            {
                return null;
            }

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            //a multi target project is a Roslyn project per framework, and a file of a shared
            //project belongs to every project which includes it: everything with this file path
            var projects = context.Workspace.CurrentSolution.Projects
                .Where(p => string.Equals(p.FilePath, _options.SolutionPath, StringComparison.OrdinalIgnoreCase));

            foreach (var project in projects)
            {
                foreach (var document in project.Documents)
                {
                    Add(document.FilePath);
                }

                foreach (var document in project.AdditionalDocuments)
                {
                    Add(document.FilePath);
                }
            }

            //the xaml files, which Roslyn knows nothing about, come from the solution tree
            foreach (var pair in await context.Solution.GetProjectOfEveryFileAsync())
            {
                if (string.Equals(pair.Value.FilePath, _options.SolutionPath, StringComparison.OrdinalIgnoreCase))
                {
                    Add(pair.Key);
                }
            }

            return result;

            void Add(string? filePath)
            {
                if (!string.IsNullOrEmpty(filePath))
                {
                    result.Add(filePath!);
                }
            }
        }

        /// <summary>
        /// Adjust the collected files and remove the using clauses of the namespaces which
        /// became empty.
        /// </summary>
        private async Task<ExitCode> AdjustAsync(
            AdjustContext context,
            IReadOnlyList<string> subjectFilePaths,
            string solutionFolder,
            CancellationToken cancellationToken
            )
        {
            var session = new AdjustSession(
                context,
                _options.ReplaceRegex,
                //an undoable change requires an opened editor and there is none here
                openFilesToEnableUndo: false
                );

            var outcome = await session.RunAsync(
                subjectFilePaths,
                new ConsoleProgress(_output, solutionFolder, _options.Verbose),
                cancellationToken
                );

            if (outcome == AdjustSessionOutcome.Cancelled)
            {
                //the files which have been adjusted before the cancel are changed already
                _error.WriteLine("Cancelled. The changes which were applied before the cancel are kept.");

                return ExitCode.Error;
            }

            _output.WriteLine($"Done, {subjectFilePaths.Count} file(s) adjusted.");

            return ExitCode.Success;
        }

        /// <summary>
        /// Compile every project and report the errors. The adjusting is based on the semantic
        /// model, so a broken solution may lead to incorrect results — this is the first step
        /// of the wizard, with the difference that the console utility stops instead of asking.
        /// </summary>
        /// <returns><c>false</c> if the run has to be given up.</returns>
        private async Task<bool> CompilesAsync(
            Workspace workspace,
            CancellationToken cancellationToken
            )
        {
            _output.WriteLine("Compiling the solution");

            var errors = new List<Diagnostic>();
            foreach (var project in workspace.CurrentSolution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation is null)
                {
                    continue;
                }

                errors.AddRange(
                    compilation.GetDiagnostics(cancellationToken)
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                    );
            }

            if (errors.Count == 0)
            {
                return true;
            }

            foreach (var error in errors.Take(MaxReportedCompilationErrors))
            {
                _error.WriteLine($"  {error}");
            }

            if (errors.Count > MaxReportedCompilationErrors)
            {
                _error.WriteLine($"  ... and {errors.Count - MaxReportedCompilationErrors} more");
            }

            if (_options.Force)
            {
                _error.WriteLine(
                    $"warning: the solution has {errors.Count} compilation error(s), the result may be incorrect."
                    );

                return true;
            }

            _error.WriteLine(
                $"error: the solution has {errors.Count} compilation error(s). "
                + "The adjusting is based on the semantic model and would produce incorrect results; "
                + "fix the errors or run with --force."
                );

            return false;
        }

        private void ReportLoadFailures(
            IReadOnlyList<string> loadFailures
            )
        {
            foreach (var failure in loadFailures)
            {
                _error.WriteLine($"warning: {failure}");
            }
        }

        private void ReportPlan(
            IReadOnlyList<string> subjectFilePaths,
            string solutionFolder
            )
        {
            _output.WriteLine($"{subjectFilePaths.Count} file(s) have to be adjusted:");

            foreach (var subjectFilePath in subjectFilePaths)
            {
                _output.WriteLine($"  {RelativePath.Of(subjectFilePath, solutionFolder)}");
            }
        }
    }
}
