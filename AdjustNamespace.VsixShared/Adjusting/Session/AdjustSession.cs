using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Namespace;
using AdjustNamespace.Roslyn;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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
    /// </summary>
    public sealed class AdjustSession
    {
        private readonly AdjustContext _context;
        private readonly NamespaceReplaceRegex _replaceRegex;
        private readonly bool _openFilesToEnableUndo;

        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        /// <param name="openFilesToEnableUndo">Open the changed files in the editor (this allows the user to undo the changes).</param>
        public AdjustSession(
            AdjustContext context,
            NamespaceReplaceRegex replaceRegex,
            bool openFilesToEnableUndo
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
            _openFilesToEnableUndo = openFilesToEnableUndo;
        }

        /// <summary>
        /// Perform the whole session: the files first, the cleanup then.
        ///
        /// A cancelled session is a usual outcome and not an error: the changes which have
        /// been applied already are not reverted (this is what the wizard promises the user),
        /// so the solution stays in the state the cancel has found it in.
        /// </summary>
        /// <param name="subjectFilePaths">
        /// Full paths of the files to adjust, in the order they have to be processed.
        /// A file which is no subject to change is skipped, see <see cref="AdjusterFactory"/>.
        /// </param>
        /// <param name="progress">Sink of the progress reports, or <c>null</c> if nobody watches.</param>
        /// <param name="cancellationToken">Cancellation of the session.</param>
        public async Task<AdjustSessionOutcome> RunAsync(
            IReadOnlyList<string> subjectFilePaths,
            IProgress<AdjustProgress>? progress = null,
            CancellationToken cancellationToken = default
            )
        {
            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            try
            {
                //the state of the namespaces is shared by all the files of the session:
                //a namespace is empty only after the last file of it has been moved away
                var namespaceCenter = await NamespaceCenter.CreateForAsync(_context.Workspace);

                var adjusterFactory = await AdjusterFactory.CreateAsync(
                    _context,
                    _replaceRegex,
                    _openFilesToEnableUndo,
                    namespaceCenter
                    );

                await AdjustAsync(adjusterFactory, subjectFilePaths, progress, cancellationToken);

                await CleanupAsync(namespaceCenter, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return AdjustSessionOutcome.Cancelled;
            }

            return AdjustSessionOutcome.Completed;
        }

        /// <summary>
        /// Adjust the chosen files one by one.
        /// </summary>
        private static async Task AdjustAsync(
            AdjusterFactory adjusterFactory,
            IReadOnlyList<string> subjectFilePaths,
            IProgress<AdjustProgress>? progress,
            CancellationToken cancellationToken
            )
        {
            var total = subjectFilePaths.Count;

            for (var i = 0; i < total; i++)
            {
                //a file is either adjusted completely or not touched at all: the edits of one
                //file are applied together (see EditApplier) and a half of them is a broken file
                cancellationToken.ThrowIfCancellationRequested();

                var subjectFilePath = subjectFilePaths[i];

                progress?.Report(
                    new AdjustProgress(AdjustStage.Adjusting, i + 1, total, subjectFilePath)
                    );
                Debug.WriteLine($"----------------------------> {i} Adjust {subjectFilePath}");

                var adjuster = await adjusterFactory.CreateAsync(subjectFilePath, cancellationToken);
                if (adjuster is not null)
                {
                    await adjuster.AdjustAsync(cancellationToken);
                }
            }
        }

        /// <summary>
        /// Walk through every C# document of the solution and remove the using clauses
        /// of the namespaces which became empty during the adjusting.
        /// </summary>
        private async Task CleanupAsync(
            NamespaceCenter namespaceCenter,
            IProgress<AdjustProgress>? progress,
            CancellationToken cancellationToken
            )
        {
            var cleanupFilePaths = _context.Workspace.EnumerateAllDocumentFilePaths(
                Scope.IsProjectInScope,
                Scope.IsDocumentInScope
                );

            var cleanup = new Cleanup(_context.Workspace, namespaceCenter);

            var total = cleanupFilePaths.Count;

            for (var i = 0; i < total; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var documentFilePath = cleanupFilePaths[i];

                progress?.Report(
                    new AdjustProgress(AdjustStage.Cleanup, i + 1, total, documentFilePath)
                    );
                Debug.WriteLine($"----------------------------> {i} Cleanup {documentFilePath}");

                await cleanup.RemoveEmptyUsingStatementsForAsync(documentFilePath, cancellationToken);
            }
        }
    }
}
