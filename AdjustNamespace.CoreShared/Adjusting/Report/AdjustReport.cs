using AdjustNamespace.Adjusting.BuildChecking;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.TextReferences;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// What a run of <see cref="AdjustSession"/> has done: which of the files the user wanted
    /// adjusted are adjusted, which are not and why, and which other files have been changed on
    /// the way.
    ///
    /// The report is a plain data object of the core (no Visual Studio types) and renders itself
    /// as a Markdown (<see cref="ToMarkdown"/>) and as a JSON (<see cref="ToJson"/>) text, one
    /// renderer each. The wizard shows it, the "copy as Markdown" button puts it on the clipboard
    /// and the console utility prints and saves it, so all of them give the very same text.
    /// </summary>
    public sealed class AdjustReport
    {
        /// <summary>
        /// How the session has ended.
        /// </summary>
        public AdjustSessionOutcome Outcome
        {
            get;
        }

        /// <summary>
        /// What has gone wrong, if <see cref="Outcome"/> is <see cref="AdjustSessionOutcome.Failed"/>.
        /// </summary>
        public string? FailureMessage
        {
            get;
        }

        /// <summary>
        /// Folder of the solution, the paths of the report are shown relative to it.
        /// </summary>
        public string? SolutionFolder
        {
            get;
        }

        /// <summary>
        /// Every file the user wanted adjusted and what has happened with it.
        /// </summary>
        public IReadOnlyList<AdjustFileOutcome> Files
        {
            get;
        }

        /// <summary>
        /// Every file the run has changed (the adjusted files themselves and the files which
        /// reference the moved types), with the kinds of the changes.
        /// </summary>
        public IReadOnlyList<AdjustTouchedFile> TouchedFiles
        {
            get;
        }

        /// <summary>
        /// The files the final cleanup has changed (the using clauses of the emptied
        /// namespaces it has removed). They belong to no particular adjusted file.
        /// </summary>
        public IReadOnlyList<AdjustTouchedFile> CleanupFiles
        {
            get;
        }

        /// <summary>
        /// The text references to the moved types the search has shown or the run has changed,
        /// each with what has been done with it. Empty if the search was off.
        /// </summary>
        public IReadOnlyList<TextReferenceResult> TextReferences
        {
            get;
        }

        /// <summary>
        /// The search of the text references was performed (its mode was not <c>Off</c>).
        /// </summary>
        public bool TextReferencesWereSearched
        {
            get;
        }

        /// <summary>
        /// How many unlikely matches were found and hidden.
        /// </summary>
        public int HiddenTextReferencesCount
        {
            get;
        }

        /// <summary>
        /// What the reader of the report has to know besides the lists: serialized data
        /// elsewhere, a failed build check.
        /// </summary>
        public IReadOnlyList<string> Notes
        {
            get;
        }

        /// <summary>
        /// The compilation errors before and after the run, if the build check was asked for;
        /// <c>null</c> otherwise.
        /// </summary>
        public BuildCheckResult? BuildCheck
        {
            get;
        }

        /// <summary>
        /// How many text references have been changed.
        /// </summary>
        public int TextChangedCount => TextReferences.Count(t => t.IsChanged);

        /// <summary>
        /// How many text references have been left as they are.
        /// </summary>
        public int TextLeftCount => TextReferences.Count(t => !t.IsChanged);

        /// <summary>
        /// How many files have been adjusted.
        /// </summary>
        public int AdjustedCount => CountOf(AdjustFileStatus.Adjusted);

        /// <summary>
        /// How many files cannot be adjusted.
        /// </summary>
        public int BlockedCount => CountOf(AdjustFileStatus.Blocked);

        /// <summary>
        /// How many files had nothing to move.
        /// </summary>
        public int SkippedCount => CountOf(AdjustFileStatus.Skipped);

        /// <summary>
        /// How many files the session has not got to (it has been cancelled or has failed).
        /// </summary>
        public int NotProcessedCount => CountOf(AdjustFileStatus.NotProcessed);

        /// <summary>
        /// How many files have failed.
        /// </summary>
        public int FailedCount => CountOf(AdjustFileStatus.Failed);

        /// <summary>
        /// Something was not adjusted or was left: a file is blocked, not processed or failed,
        /// or the session has not ended normally.
        /// </summary>
        public bool HasLeftovers =>
            Outcome != AdjustSessionOutcome.Completed
            || BlockedCount > 0
            || NotProcessedCount > 0
            || FailedCount > 0
            || TextLeftCount > 0
            || (BuildCheck != null && BuildCheck.NewErrors.Count > 0)
            ;

        /// <summary>
        /// The files with the given status, in the order they were given to the session.
        /// </summary>
        public IEnumerable<AdjustFileOutcome> FilesWith(AdjustFileStatus status)
        {
            return Files.Where(f => f.Status == status);
        }

        internal AdjustReport(
            AdjustSessionOutcome outcome,
            string? failureMessage,
            string? solutionFolder,
            IReadOnlyList<AdjustFileOutcome> files,
            IReadOnlyList<AdjustTouchedFile> touchedFiles,
            IReadOnlyList<AdjustTouchedFile> cleanupFiles,
            IReadOnlyList<TextReferenceResult> textReferences,
            bool textReferencesWereSearched,
            int hiddenTextReferencesCount,
            IReadOnlyList<string> notes,
            BuildCheckResult? buildCheck
            )
        {
            if (files is null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            if (textReferences is null)
            {
                throw new ArgumentNullException(nameof(textReferences));
            }

            if (notes is null)
            {
                throw new ArgumentNullException(nameof(notes));
            }

            if (touchedFiles is null)
            {
                throw new ArgumentNullException(nameof(touchedFiles));
            }

            if (cleanupFiles is null)
            {
                throw new ArgumentNullException(nameof(cleanupFiles));
            }

            Outcome = outcome;
            FailureMessage = failureMessage;
            SolutionFolder = solutionFolder;
            Files = files;
            TouchedFiles = touchedFiles;
            CleanupFiles = cleanupFiles;
            TextReferences = textReferences;
            TextReferencesWereSearched = textReferencesWereSearched;
            HiddenTextReferencesCount = hiddenTextReferencesCount;
            Notes = notes;
            BuildCheck = buildCheck;
        }

        /// <summary>
        /// The path as the report shows it: relative to the folder of the solution, unless the
        /// file lies outside of it (a linked file).
        /// </summary>
        public string DisplayPath(string filePath)
        {
            return ReportPaths.Relative(filePath, SolutionFolder);
        }

        /// <summary>
        /// The summary line: <c>Adjusted 3 file(s), blocked 1, ...</c>.
        /// </summary>
        public string Summary
        {
            get
            {
                var parts = new List<string>
                {
                    $"adjusted {AdjustedCount} file(s)",
                    $"blocked {BlockedCount}",
                    $"skipped {SkippedCount}",
                    $"not processed {NotProcessedCount}"
                };

                if (FailedCount > 0)
                {
                    parts.Add($"failed {FailedCount}");
                }

                var text = string.Join(", ", parts);

                text = char.ToUpperInvariant(text[0]) + text.Substring(1) + ".";

                if (TextReferencesWereSearched)
                {
                    text += $" Text references: changed {TextChangedCount}, left {TextLeftCount}.";
                }

                return text;
            }
        }

        /// <summary>
        /// The report as a Markdown text.
        /// </summary>
        public string ToMarkdown()
        {
            return AdjustReportMarkdown.Render(this);
        }

        /// <summary>
        /// The report as a JSON text.
        /// </summary>
        public string ToJson()
        {
            return AdjustReportJson.Render(this);
        }

        private int CountOf(AdjustFileStatus status)
        {
            return Files.Count(f => f.Status == status);
        }
    }
}
