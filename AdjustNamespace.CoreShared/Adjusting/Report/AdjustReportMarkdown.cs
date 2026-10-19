using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.TextReferences;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// The Markdown renderer of an <see cref="AdjustReport"/>. The only one: the wizard, the
    /// "copy as Markdown" button and the console utility all print what this class writes.
    ///
    /// The text is plain ASCII apart from the names the user has chosen, so it reads well in a
    /// console with any code page.
    /// </summary>
    public static class AdjustReportMarkdown
    {
        /// <summary>
        /// Render the report.
        /// </summary>
        public static string Render(AdjustReport report)
        {
            if (report is null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var text = new StringBuilder();

            text.Append("# Adjust namespaces report\n\n");
            text.Append(report.Summary).Append('\n');

            AppendOutcome(text, report);
            AppendAdjusted(text, report);
            AppendNotAdjusted(text, report);
            AppendCleanup(text, report);
            AppendTextReferences(text, report);
            AppendBuildCheck(text, report);
            AppendNotes(text, report);

            return text.ToString();
        }

        private static void AppendBuildCheck(StringBuilder text, AdjustReport report)
        {
            var check = report.BuildCheck;
            if (check == null)
            {
                return;
            }

            text.Append("\n## Build check\n\n");
            text.Append($"Projects checked: {check.ProjectNames.Count}. ");
            text.Append($"Compilation errors before the run: {check.ErrorsBefore}, after it: {check.ErrorsAfter}, ");
            text.Append($"new: {check.NewErrors.Count}.\n");

            if (check.NewErrors.Count == 0)
            {
                return;
            }

            text.Append('\n');

            foreach (var error in check.NewErrors)
            {
                var location = error.FilePath == null
                    ? error.ProjectName
                    : $"{report.DisplayPath(error.FilePath)}:{error.Line}";

                text.Append("- ").Append(Code(location)).Append(' ')
                    .Append(error.Id).Append(": ").Append(error.Message).Append('\n');
            }
        }

        private static void AppendTextReferences(StringBuilder text, AdjustReport report)
        {
            if (!report.TextReferencesWereSearched)
            {
                return;
            }

            text.Append("\n## Text references\n\n");
            text.Append($"Changed {report.TextChangedCount}, left {report.TextLeftCount}");

            if (report.HiddenTextReferencesCount > 0)
            {
                text.Append($", {report.HiddenTextReferencesCount} unlikely not shown");
            }

            text.Append(".\n");

            AppendTextGroup(text, report, "Changed", report.TextReferences.Where(t => t.IsChanged).ToList());
            AppendTextGroup(text, report, "Left", report.TextReferences.Where(t => !t.IsChanged).ToList());
        }

        private static void AppendTextGroup(
            StringBuilder text,
            AdjustReport report,
            string title,
            List<TextReferenceResult> results
            )
        {
            if (results.Count == 0)
            {
                return;
            }

            text.Append($"\n### {title} ({results.Count})\n\n");

            foreach (var result in results)
            {
                var match = result.Match;

                text.Append("- ").Append(Code($"{report.DisplayPath(match.FilePath)}:{match.Line}"))
                    .Append(' ').Append(Code(match.MatchedText))
                    .Append(" -> ").Append(Code(match.ReplacementText))
                    .Append($" [{match.Level} {match.Score.Value}]");

                if (result.Reason != null)
                {
                    text.Append(": ").Append(result.Reason);
                }

                text.Append('\n');
                text.Append("  (").Append(match.Score.Explain()).Append(")\n");
            }
        }

        private static void AppendNotes(StringBuilder text, AdjustReport report)
        {
            if (report.Notes.Count == 0)
            {
                return;
            }

            text.Append("\n## Notes\n\n");

            foreach (var note in report.Notes)
            {
                text.Append("- ").Append(note).Append('\n');
            }
        }

        private static void AppendOutcome(StringBuilder text, AdjustReport report)
        {
            if (report.Outcome == AdjustSessionOutcome.Cancelled)
            {
                text.Append("\nThe run has been cancelled. The changes which were applied before the cancel are kept.\n");
            }
            else if (report.Outcome == AdjustSessionOutcome.Failed)
            {
                text.Append("\nThe run has failed: ").Append(report.FailureMessage ?? "unknown error").Append('\n');
            }
        }

        private static void AppendAdjusted(StringBuilder text, AdjustReport report)
        {
            var adjusted = report.FilesWith(AdjustFileStatus.Adjusted).ToList();
            if (adjusted.Count == 0)
            {
                return;
            }

            text.Append($"\n## Adjusted ({adjusted.Count})\n");

            foreach (var file in adjusted)
            {
                text.Append("\n### ").Append(Code(report.DisplayPath(file.FilePath))).Append('\n');

                if (file.Changes.Count > 0)
                {
                    text.Append('\n');

                    foreach (var change in file.Changes)
                    {
                        text.Append("Namespace: ")
                            .Append(Code(change.Old.Length == 0 ? "(global)" : change.Old))
                            .Append(" -> ")
                            .Append(Code(change.New))
                            .Append('\n');
                    }
                }

                text.Append($"\nEdits in other files: {file.EditsInOtherFiles}\n");

                if (file.EditedFiles.Count > 0)
                {
                    text.Append('\n');

                    foreach (var edited in file.EditedFiles)
                    {
                        text.Append("- ").Append(Code(report.DisplayPath(edited.FilePath)))
                            .Append(": ").Append(edited.DescribeEdits()).Append('\n');
                    }
                }
            }
        }

        private static void AppendNotAdjusted(StringBuilder text, AdjustReport report)
        {
            var blocked = report.FilesWith(AdjustFileStatus.Blocked).ToList();
            var skipped = report.FilesWith(AdjustFileStatus.Skipped).ToList();
            var notProcessed = report.FilesWith(AdjustFileStatus.NotProcessed).ToList();
            var failed = report.FilesWith(AdjustFileStatus.Failed).ToList();

            if (blocked.Count + skipped.Count + notProcessed.Count + failed.Count == 0)
            {
                return;
            }

            text.Append("\n## Not adjusted\n");

            AppendGroup(text, report, "Blocked", blocked, true);
            AppendGroup(text, report, "Skipped (nothing to move)", skipped, false);
            AppendGroup(text, report, "Not processed (the run did not get to them)", notProcessed, false);
            AppendGroup(text, report, "Failed", failed, true);
        }

        private static void AppendGroup(
            StringBuilder text,
            AdjustReport report,
            string title,
            List<AdjustFileOutcome> files,
            bool withMessage
            )
        {
            if (files.Count == 0)
            {
                return;
            }

            text.Append($"\n### {title} ({files.Count})\n\n");

            foreach (var file in files)
            {
                text.Append("- ").Append(Code(report.DisplayPath(file.FilePath)));

                if (withMessage && !string.IsNullOrEmpty(file.Message))
                {
                    text.Append(": ").Append(file.Message);
                }

                text.Append('\n');
            }
        }

        private static void AppendCleanup(StringBuilder text, AdjustReport report)
        {
            if (report.CleanupFiles.Count == 0)
            {
                return;
            }

            text.Append($"\n## Cleanup ({report.CleanupFiles.Count})\n\n");

            foreach (var file in report.CleanupFiles)
            {
                text.Append("- ").Append(Code(report.DisplayPath(file.FilePath)))
                    .Append(": ").Append(file.DescribeEdits()).Append('\n');
            }
        }

        /// <summary>
        /// The text as an inline code span (a pipe would break a table cell).
        /// </summary>
        internal static string Code(string value)
        {
            return "`" + value.Replace("|", "\\|").Replace("`", "'") + "`";
        }
    }
}
