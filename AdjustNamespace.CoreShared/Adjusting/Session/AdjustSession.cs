using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Adjusting.BuildChecking;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Adjusting.Report;
using AdjustNamespace.Namespace;
using AdjustNamespace.Roslyn;
using AdjustNamespace.TextReferences;
using AdjustNamespace.Xaml;
using AdjustNamespace.Xaml.BodyProvider;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdjustNamespace;

namespace AdjustNamespace.Adjusting.Session
{
    /// <summary>
    /// One run of the extension over the files the user has chosen: every file is adjusted
    /// (see <see cref="AdjusterFactory"/>), and the using clauses of the namespaces which
    /// became empty are removed from the whole solution afterwards (see <see cref="Cleanup"/>).
    ///
    /// This used to be the body of <c>PerformingViewModel</c>, i.e. a part of the wizard:
    /// the order of the stages, the <see cref="NamespaceCenter"/> shared by all the files of
    /// one run and the reaction to a cancel were observable in a running Visual Studio only.
    /// Now the wizard owns the window and this class owns the session.
    ///
    /// The session answers with an <see cref="AdjustReport"/>: what has been adjusted, what
    /// has not and why, and which files have been changed on the way.
    /// </summary>
    public sealed class AdjustSession
    {
        private readonly AdjustContext _context;
        private readonly NamespaceReplaceRegex _replaceRegex;
        private readonly IReadOnlyList<AdjustBlock> _blocked;
        private readonly TextReferenceOptions _textReferenceOptions;
        private readonly TextReferenceScanResult? _textReferenceScan;
        private readonly bool _runBuildCheck;

        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        /// <param name="blocked">
        /// The files the scan before the session has found impossible to adjust. They are not a
        /// part of the session, but the report has to say that they were left.
        /// </param>
        /// <param name="textReferenceOptions">
        /// How aggressively the text references to the moved types are searched and changed;
        /// <c>null</c> (the default) is no search at all, which is what the sessions that are not
        /// asked for it do.
        /// </param>
        /// <param name="textReferenceScan">
        /// The search of the text references the wizard has already made and the user has
        /// reviewed: its approved matches are changed. <c>null</c> makes the session search by
        /// itself, with <paramref name="textReferenceOptions"/>.
        /// </param>
        /// <param name="runBuildCheck">
        /// Count the compilation errors of the solution before the run and after it, and put the
        /// new ones into the report.
        /// </param>
        public AdjustSession(
            AdjustContext context,
            NamespaceReplaceRegex replaceRegex,
            IReadOnlyList<AdjustBlock>? blocked = null,
            TextReferenceOptions? textReferenceOptions = null,
            TextReferenceScanResult? textReferenceScan = null,
            bool runBuildCheck = false
            )
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            _context = context;
            _replaceRegex = replaceRegex;
            _blocked = blocked ?? Array.Empty<AdjustBlock>();
            _textReferenceOptions = textReferenceScan?.Options ?? textReferenceOptions ?? TextReferenceOptions.Off;
            _textReferenceScan = textReferenceScan;
            _runBuildCheck = runBuildCheck;
        }

        /// <summary>
        /// Perform the whole session: the files first, the cleanup then.
        ///
        /// A cancelled session is a usual outcome and not an error: the changes which have
        /// been applied already are not reverted (this is what the wizard promises the user),
        /// so the solution stays in the state the cancel has found it in.
        ///
        /// A file which fails unexpectedly stops the session: the report is
        /// <see cref="AdjustSessionOutcome.Failed"/>, it names the file and the message, and
        /// the files which were not reached are not processed.
        /// </summary>
        /// <param name="subjectFilePaths">
        /// Full paths of the files to adjust, in the order they have to be processed.
        /// A file which is no subject to change is skipped, see <see cref="AdjusterFactory"/>.
        /// </param>
        /// <param name="progress">Sink of the progress reports, or <c>null</c> if nobody watches.</param>
        /// <param name="cancellationToken">Cancellation of the session.</param>
        public async Task<AdjustReport> RunAsync(
            IReadOnlyList<string> subjectFilePaths,
            IProgress<AdjustProgress>? progress = null,
            CancellationToken cancellationToken = default
            )
        {
            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            LogRoslynVersion();

            AdjustLog.WriteLine($"[Adjust] Session started, {subjectFilePaths.Count} subject file(s)");

            var report = new AdjustReportBuilder(_context.Settings.SolutionFolder);

            foreach (var block in _blocked)
            {
                report.RecordFile(AdjustFileOutcome.Blocked(block));
            }

            var outcome = AdjustSessionOutcome.Completed;
            string? failure = null;

            IReadOnlyList<CompilationErrorInfo>? errorsBefore = null;

            try
            {
                if (_runBuildCheck)
                {
                    progress?.Report(new AdjustProgress(AdjustStage.BuildCheck, 1, 2, "errors before the run"));

                    errorsBefore = await BuildChecker.CaptureAsync(_context.Workspace, null, cancellationToken);
                }

                //the text references go first: the spans of the search are valid against the
                //text as it is now, and a run which is cancelled half way leaves the names in
                //the strings in the state the next run finds the types in
                await ChangeTextReferencesAsync(subjectFilePaths, progress, report, cancellationToken);

                //the state of the namespaces is shared by all the files of the session:
                //a namespace is empty only after the last file of it has been moved away
                var namespaceCenter = await NamespaceCenter.CreateForAsync(_context.Workspace);

                var adjusterFactory = await AdjusterFactory.CreateAsync(
                    _context,
                    _replaceRegex,
                    namespaceCenter,
                    report
                    );

                var adjusting = await AdjustAsync(adjusterFactory, subjectFilePaths, progress, report, cancellationToken);
                outcome = adjusting.Outcome;
                failure = adjusting.Failure;

                if (outcome == AdjustSessionOutcome.Completed)
                {
                    await CleanupAsync(namespaceCenter, progress, report, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                outcome = AdjustSessionOutcome.Cancelled;
            }
            catch (Exception exception)
            {
                AdjustLog.WriteLine($"[Adjust] Session failed: {exception}");

                outcome = AdjustSessionOutcome.Failed;
                failure = exception.Message;
            }

            //the check is made over whatever the run has changed, a cancelled run included
            if (errorsBefore != null && outcome != AdjustSessionOutcome.Failed)
            {
                try
                {
                    progress?.Report(new AdjustProgress(AdjustStage.BuildCheck, 2, 2, "errors after the run"));

                    var projectNames = BuildChecker.AffectedProjectNames(_context.Workspace, report.TouchedFilePaths);
                    var errorsAfter = await BuildChecker.CaptureAsync(
                        _context.Workspace,
                        ChangedStartupObjects(report),
                        CancellationToken.None
                        );

                    report.RecordBuildCheck(BuildChecker.Compare(projectNames, errorsBefore, errorsAfter));
                }
                catch (Exception exception)
                {
                    AdjustLog.WriteLine($"[Adjust] Build check failed: {exception}");

                    report.AddNote($"The build check has failed: {exception.Message}");
                }
            }

            //whatever has not been reached is not processed
            foreach (var subjectFilePath in subjectFilePaths)
            {
                if (!report.IsRecorded(subjectFilePath))
                {
                    report.RecordFile(AdjustFileOutcome.NotProcessed(subjectFilePath));
                }
            }

            AdjustLog.WriteLine($"[Adjust] Session {outcome.ToString().ToLowerInvariant()}");

            return report.Build(outcome, failure);
        }

        /// <summary>
        /// The <c>StartupObject</c> the run has written into the project files, keyed by the
        /// project file: the build check compiles the projects with it.
        /// </summary>
        private static Dictionary<string, string> ChangedStartupObjects(AdjustReportBuilder report)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var changed in report.TextReferenceResults)
            {
                //only a startup object that is the name and nothing else (not `A.B.Outer+Inner`)
                if (changed.IsChanged
                    && changed.Match.Place == TextReferencePlace.ProjectStartupObject
                    && changed.Match.Score.Signals.Any(s => s.Name == ScoreSignals.ExactName.Name))
                {
                    result[changed.Match.FilePath] = changed.Match.ReplacementText;
                }
            }

            return result;
        }

        /// <summary>
        /// Search for the text references to the types the session moves (unless the wizard has
        /// done it already) and change the approved ones.
        /// </summary>
        private async Task ChangeTextReferencesAsync(
            IReadOnlyList<string> subjectFilePaths,
            IProgress<AdjustProgress>? progress,
            AdjustReportBuilder report,
            CancellationToken cancellationToken
            )
        {
            var scan = _textReferenceScan;

            if (scan == null)
            {
                if (!_textReferenceOptions.IsScanEnabled)
                {
                    return;
                }

                scan = await TextReferenceScanner.ScanAsync(
                    _context,
                    _replaceRegex,
                    subjectFilePaths,
                    _textReferenceOptions,
                    progress,
                    cancellationToken
                    );
            }

            if (!scan.WasPerformed)
            {
                return;
            }

            var results = await new TextReferenceApplier(_context.Workspace, _context.XamlBodyProviderFactory)
                .ApplyAsync(scan, cancellationToken);

            report.RecordTextReferences(results, scan.HiddenCount);

            AdjustLog.WriteLine(
                $"[Adjust] Text references: {results.Count(r => r.IsChanged)} changed, {results.Count(r => !r.IsChanged)} left"
                );
        }

        /// <summary>
        /// Logs the version of the Roslyn assemblies the session runs against: the extension
        /// (<c>AdjustNamespace.2022</c>) and the test suite reference different Roslyn versions,
        /// see the note in CLAUDE.md, and this is the only way to tell which one is in effect
        /// when a bug is reported from a running Visual Studio.
        /// </summary>
        private static void LogRoslynVersion()
        {
            var workspacesAssembly = typeof(Workspace).Assembly.GetName();
            var csharpAssembly = typeof(Microsoft.CodeAnalysis.CSharp.SyntaxFactory).Assembly.GetName();

            AdjustLog.WriteLine(
                $"[Adjust] Roslyn: Microsoft.CodeAnalysis.Workspaces={workspacesAssembly.Version}, "
                + $"Microsoft.CodeAnalysis.CSharp={csharpAssembly.Version}"
                );
        }

        /// <summary>
        /// Adjust the chosen files one by one.
        /// </summary>
        private async Task<StageResult> AdjustAsync(
            AdjusterFactory adjusterFactory,
            IReadOnlyList<string> subjectFilePaths,
            IProgress<AdjustProgress>? progress,
            AdjustReportBuilder report,
            CancellationToken cancellationToken
            )
        {
            var total = subjectFilePaths.Count;

            for (var i = 0; i < total; i++)
            {
                var subjectFilePath = subjectFilePaths[i];

                try
                {
                    //a file is either adjusted completely or not touched at all: the edits of one
                    //file are applied together (see EditApplier) and a half of them is a broken file
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report(
                        new AdjustProgress(AdjustStage.Adjusting, i + 1, total, subjectFilePath)
                        );
                    AdjustLog.WriteLine($"[Adjust] ----------------------------> {i} Adjust {subjectFilePath}");

                    await AdjustFileAsync(adjusterFactory, subjectFilePath, report, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    AdjustLog.WriteLine("[Adjust] Session cancelled");

                    //this file and the ones behind it are not processed, see RunAsync
                    return new StageResult(AdjustSessionOutcome.Cancelled, null);
                }
                catch (Exception exception)
                {
                    AdjustLog.WriteLine($"[Adjust] Processing of {subjectFilePath} failed: {exception}");

                    report.RecordFile(AdjustFileOutcome.Failed(subjectFilePath, exception.Message));

                    return new StageResult(
                        AdjustSessionOutcome.Failed,
                        $"{exception.Message} ({report.DisplayPath(subjectFilePath)})"
                        );
                }
            }

            return new StageResult(AdjustSessionOutcome.Completed, null);
        }

        /// <summary>
        /// Adjust one file and tell the report what has happened to it.
        /// </summary>
        private static async Task AdjustFileAsync(
            AdjusterFactory adjusterFactory,
            string subjectFilePath,
            AdjustReportBuilder report,
            CancellationToken cancellationToken
            )
        {
            var planResult = await adjusterFactory.PlanAsync(subjectFilePath, cancellationToken);
            if (planResult.HasBlock)
            {
                report.RecordFile(AdjustFileOutcome.Blocked(planResult.Block!.Value));
                return;
            }

            if (!planResult.HasPlan)
            {
                report.RecordFile(AdjustFileOutcome.Skipped(subjectFilePath, "Nothing to move."));
                return;
            }

            var plan = planResult.Plan!.Value;

            //a xaml file has to be read before it is changed
            var changes = ChangesOf(plan);

            var adjuster = adjusterFactory.Create(plan);

            if (await adjuster.AdjustAsync(cancellationToken))
            {
                report.RecordFile(AdjustFileOutcome.Adjusted(subjectFilePath, changes));
            }
            else
            {
                report.RecordFile(AdjustFileOutcome.Skipped(subjectFilePath, "Nothing to move."));
            }
        }

        /// <summary>
        /// The namespaces the plan moves the file from and into.
        /// </summary>
        private static IReadOnlyList<NamespaceChange> ChangesOf(AdjustPlanItem plan)
        {
            if (plan.IsXaml)
            {
                var oldNamespace = TryReadRootNamespaceOfXaml(plan.FilePath);

                return oldNamespace is null
                    ? Array.Empty<NamespaceChange>()
                    : new[] { new NamespaceChange(oldNamespace, plan.TargetNamespace) };
            }

            return plan.Transitions.Transitions
                .Where(t => t.IsRoot && t.OriginalName != t.ModifiedName)
                .Select(t => new NamespaceChange(t.OriginalName, t.ModifiedName))
                .ToList();
        }

        /// <summary>
        /// The namespace of the <c>x:Class</c> of a xaml file, or <c>null</c> if it cannot be read.
        /// </summary>
        private static string? TryReadRootNamespaceOfXaml(string xamlFilePath)
        {
            try
            {
                using var document = new XamlDocument(new ClosedXamlBodyProvider(xamlFilePath));

                return document.GetRootInfo(out var rootNamespace, out _)
                    ? rootNamespace ?? string.Empty
                    : null;
            }
            catch (System.IO.IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Walk through every C# document of the solution and remove the using clauses
        /// of the namespaces which became empty during the adjusting.
        /// </summary>
        private async Task CleanupAsync(
            NamespaceCenter namespaceCenter,
            IProgress<AdjustProgress>? progress,
            AdjustReportBuilder report,
            CancellationToken cancellationToken
            )
        {
            var cleanupFilePaths = _context.Workspace.EnumerateAllDocumentFilePaths(
                Scope.IsProjectInScope,
                Scope.IsDocumentInScope
                );

            var cleanup = new Cleanup(_context.Workspace, namespaceCenter, report);

            var total = cleanupFilePaths.Count;

            for (var i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var documentFilePath = cleanupFilePaths[i];

                progress?.Report(
                    new AdjustProgress(AdjustStage.Cleanup, i + 1, total, documentFilePath)
                    );
                AdjustLog.WriteLine($"[Adjust] ----------------------------> {i} Cleanup {documentFilePath}");

                await cleanup.RemoveEmptyUsingStatementsForAsync(documentFilePath, cancellationToken);
            }
        }

        /// <summary>
        /// How a stage of the session has ended.
        /// </summary>
        private readonly struct StageResult
        {
            public readonly AdjustSessionOutcome Outcome;

            public readonly string? Failure;

            public StageResult(AdjustSessionOutcome outcome, string? failure)
            {
                Outcome = outcome;
                Failure = failure;
            }
        }
    }
}
