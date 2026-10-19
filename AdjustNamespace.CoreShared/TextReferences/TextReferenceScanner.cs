using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The search of the text references to the moved types: what the types are (the plans of
    /// the session know that before anything is changed), which files may name them, and what
    /// the scanners find there.
    ///
    /// It only reads. What it finds is a <see cref="TextReferenceScanResult"/>: the review step
    /// of the wizard shows it, the session changes the approved matches
    /// (<see cref="TextReferenceApplier"/>).
    /// </summary>
    public static class TextReferenceScanner
    {
        /// <summary>
        /// Search for the text references to the types the given files are going to move.
        /// </summary>
        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        /// <param name="subjectFilePaths">The files the session is going to adjust.</param>
        /// <param name="options">How aggressive the search is.</param>
        /// <param name="progress">Sink of the progress reports, or <c>null</c>.</param>
        /// <param name="cancellationToken">Cancellation of the session.</param>
        public static async Task<TextReferenceScanResult> ScanAsync(
            AdjustContext context,
            NamespaceReplaceRegex replaceRegex,
            IReadOnlyList<string> subjectFilePaths,
            TextReferenceOptions options,
            IProgress<AdjustProgress>? progress = null,
            CancellationToken cancellationToken = default
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

            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!options.IsScanEnabled || subjectFilePaths.Count == 0)
            {
                return TextReferenceScanResult.Empty(options);
            }

            var planner = new AdjustPlanner(context, replaceRegex);

            var plans = new List<AdjustPlanItem>();
            foreach (var subjectFilePath in subjectFilePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var planResult = await planner.PlanAsync(subjectFilePath, cancellationToken);
                if (planResult.HasPlan)
                {
                    plans.Add(planResult.Plan!.Value);
                }
            }

            var moved = await MovedTypeCollector.CollectAsync(context.Workspace, plans, cancellationToken);

            return await ScanAsync(context, moved, options, progress, cancellationToken);
        }

        /// <summary>
        /// Search for the text references to the given types.
        /// </summary>
        public static async Task<TextReferenceScanResult> ScanAsync(
            AdjustContext context,
            MovedTypeSet moved,
            TextReferenceOptions options,
            IProgress<AdjustProgress>? progress = null,
            CancellationToken cancellationToken = default
            )
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (moved is null)
            {
                throw new ArgumentNullException(nameof(moved));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (!options.IsScanEnabled || moved.IsEmpty)
            {
                return TextReferenceScanResult.Empty(options);
            }

            var analyzer = new TextReferenceAnalyzer(moved);

            var csharpPaths = context.Workspace
                .EnumerateAllDocumentFilePaths(Scope.IsProjectInScope, Scope.IsDocumentInScope)
                .Where(p => !GeneratedCode.IsGeneratedFile(p))
                .ToList();

            var otherFiles = await ClassifyOtherFilesAsync(context, options);

            var matches = new List<TextReferenceMatch>();
            var total = csharpPaths.Count + otherFiles.Count;

            for (var i = 0; i < csharpPaths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new AdjustProgress(AdjustStage.TextReferences, i + 1, total, csharpPaths[i]));

                matches.AddRange(
                    await ScanCSharpFileAsync(context.Workspace, analyzer, csharpPaths[i], options, cancellationToken)
                    );
            }

            for (var i = 0; i < otherFiles.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(
                    new AdjustProgress(AdjustStage.TextReferences, csharpPaths.Count + i + 1, total, otherFiles[i].FilePath)
                    );

                matches.AddRange(
                    await ScanOtherFileAsync(context, analyzer, otherFiles[i].FilePath, otherFiles[i].Kind, options)
                    );
            }

            return Finish(matches, options);
        }

        /// <summary>
        /// Search for the names in the text of a file of any kind, without a workspace. A C# file
        /// is parsed and has no semantic model.
        /// </summary>
        internal static List<TextReferenceMatch> ScanText(
            MovedTypeSet moved,
            TextReferenceFileKind kind,
            string filePath,
            string text,
            TextReferenceOptions options
            )
        {
            if (kind == TextReferenceFileKind.CSharp)
            {
                return ScanCSharpText(moved, filePath, text, (options.Kinds & TextReferenceKinds.Comments) != 0);
            }

            var analyzer = new TextReferenceAnalyzer(moved);

            return analyzer.Analyze(filePath, kind, text, CandidatesOf(kind, text, options));
        }

        /// <summary>
        /// The parts of a file of a kind other than C# that may name a type.
        /// </summary>
        private static List<TextCandidate> CandidatesOf(
            TextReferenceFileKind kind,
            string text,
            TextReferenceOptions options
            )
        {
            switch (kind)
            {
                case TextReferenceFileKind.Razor:
                    return RazorTextScanner.Scan(text);

                case TextReferenceFileKind.Project:
                    return ProjectTextScanner.ScanProject(text);

                case TextReferenceFileKind.Settings:
                    return ProjectTextScanner.ScanSettings(text);

                case TextReferenceFileKind.XmlConfig:
                    return XmlConfigTextScanner.Scan(text, (options.Kinds & TextReferenceKinds.Comments) != 0);

                case TextReferenceFileKind.Json:
                    return JsonTextScanner.Scan(text);

                default:
                    return new List<TextCandidate>();
            }
        }

        /// <summary>
        /// The files besides the C# documents the search looks into, with their kinds.
        /// </summary>
        private static async Task<List<(string FilePath, TextReferenceFileKind Kind)>> ClassifyOtherFilesAsync(
            AdjustContext context,
            TextReferenceOptions options
            )
        {
            var result = new List<(string, TextReferenceFileKind)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in await context.Solution.GetTextFilePathsAsync())
            {
                var kind = TextReferenceFiles.Classify(filePath, options, context.Settings.SolutionFolder);

                if (kind.HasValue && seen.Add(filePath))
                {
                    result.Add((filePath, kind.Value));
                }
            }

            return result;
        }

        private static async Task<List<TextReferenceMatch>> ScanOtherFileAsync(
            AdjustContext context,
            TextReferenceAnalyzer analyzer,
            string filePath,
            TextReferenceFileKind kind,
            TextReferenceOptions options
            )
        {
            string text;

            try
            {
                text = (await context.XamlBodyProviderFactory.CreateForReadAsync(filePath)).ReadText();
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
            {
                //a file which cannot be read cannot name anything
                return new List<TextReferenceMatch>();
            }

            if (!analyzer.MayContainNames(text))
            {
                return new List<TextReferenceMatch>();
            }

            return analyzer.Analyze(filePath, kind, text, CandidatesOf(kind, text, options));
        }

        /// <summary>
        /// Search for the names in the text of a C# file. There is no semantic model, so a
        /// string that is passed as a type name is told by the name of the method only.
        /// </summary>
        internal static List<TextReferenceMatch> ScanCSharpText(
            MovedTypeSet moved,
            string filePath,
            string text,
            bool includeComments
            )
        {
            var analyzer = new TextReferenceAnalyzer(moved);

            var root = CSharpSyntaxTree.ParseText(text).GetRoot();

            var candidates = CSharpTextScanner.Scan(root, () => null, includeComments);

            return analyzer.Analyze(filePath, TextReferenceFileKind.CSharp, text, candidates);
        }

        /// <summary>
        /// Number the matches, put them in the order of the files and of the positions and
        /// preset their approval by the mode.
        /// </summary>
        internal static TextReferenceScanResult Finish(
            List<TextReferenceMatch> matches,
            TextReferenceOptions options
            )
        {
            var ordered = matches
                .OrderBy(m => m.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.Span.Start)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].Id = i + 1;
            }

            var result = new TextReferenceScanResult(ordered, options);
            result.ApproveByMode();

            return result;
        }

        private static async Task<List<TextReferenceMatch>> ScanCSharpFileAsync(
            Workspace workspace,
            TextReferenceAnalyzer analyzer,
            string filePath,
            TextReferenceOptions options,
            CancellationToken cancellationToken
            )
        {
            var documents = workspace.GetDocuments(filePath);
            if (documents.Count == 0)
            {
                return new List<TextReferenceMatch>();
            }

            var text = (await documents[0].GetTextAsync(cancellationToken)).ToString();

            //nothing to look for in a file that does not name any of the types
            if (!analyzer.MayContainTypes(text))
            {
                return new List<TextReferenceMatch>();
            }

            var includeComments = (options.Kinds & TextReferenceKinds.Comments) != 0;

            var candidates = new List<TextCandidate>();
            var seen = new HashSet<(int, int)>();

            //every project that compiles the file parses it with its own symbols, and a string
            //under a conditional compilation symbol exists in a part of the trees only
            foreach (var document in documents)
            {
                var root = await document.GetSyntaxRootAsync(cancellationToken);
                if (root == null)
                {
                    continue;
                }

                var model = await document.GetSemanticModelAsync(cancellationToken);

                foreach (var candidate in CSharpTextScanner.Scan(root, () => model, includeComments, cancellationToken))
                {
                    if (seen.Add((candidate.Start, candidate.Length)))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            return analyzer.Analyze(filePath, TextReferenceFileKind.CSharp, text, candidates);
        }
    }
}
