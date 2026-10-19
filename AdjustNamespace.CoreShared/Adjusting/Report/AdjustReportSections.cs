using AdjustNamespace.TextReferences;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// The tree the wizard shows an <see cref="AdjustReport"/> as: a section per group of files
    /// (adjusted, blocked, skipped, ...), a node per file, and below an adjusted file the files
    /// it has changed.
    ///
    /// It lives in the core because it decides what the user is told and in which words, and the
    /// wizard has no automated tests.
    /// </summary>
    public static class AdjustReportSections
    {
        /// <summary>
        /// Build the tree. A section without files is not there.
        /// </summary>
        public static IReadOnlyList<ReportItem> Build(AdjustReport report)
        {
            if (report is null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var sections = new List<ReportItem>();

            AddAdjusted(sections, report);

            AddGroup(sections, report, AdjustFileStatus.Blocked, "Blocked", true);
            AddGroup(sections, report, AdjustFileStatus.Failed, "Failed", true);
            AddGroup(sections, report, AdjustFileStatus.NotProcessed, "Not processed", false);
            AddGroup(sections, report, AdjustFileStatus.Skipped, "Skipped (nothing to move)", false);

            AddTextReferences(sections, report, true, "Text references changed");
            AddTextReferences(sections, report, false, "Text references left");

            AddCleanup(sections, report);

            AddBuildCheck(sections, report);

            if (report.Notes.Count > 0)
            {
                sections.Add(
                    new ReportItem(
                        $"Notes ({report.Notes.Count})",
                        children: report.Notes.Select(n => new ReportItem(n)).ToList(),
                        isExpanded: true
                        )
                    );
            }

            return sections;
        }

        private static void AddBuildCheck(List<ReportItem> sections, AdjustReport report)
        {
            var check = report.BuildCheck;
            if (check == null)
            {
                return;
            }

            var items = check.NewErrors.Select(error => new ReportItem(
                error.FilePath == null
                    ? error.ProjectName
                    : $"{report.DisplayPath(error.FilePath)}:{error.Line}",
                $"{error.Id} {error.Message}",
                error.FilePath,
                error.Line
                )).ToList();

            sections.Add(
                new ReportItem(
                    $"Build check: {check.NewErrors.Count} new error(s)",
                    $"errors before the run {check.ErrorsBefore}, after it {check.ErrorsAfter}; {check.ProjectNames.Count} project(s) checked",
                    children: items,
                    isExpanded: items.Count > 0
                    )
                );
        }

        private static void AddTextReferences(
            List<ReportItem> sections,
            AdjustReport report,
            bool changed,
            string title
            )
        {
            var results = report.TextReferences.Where(t => t.IsChanged == changed).ToList();
            if (results.Count == 0)
            {
                return;
            }

            var items = results.Select(r => new ReportItem(
                $"{report.DisplayPath(r.Match.FilePath)}:{r.Match.Line}",
                DescribeTextReference(r),
                r.Match.FilePath,
                r.Match.Line,
                toolTip: r.Match.LineText.Trim() + Environment.NewLine + r.Match.Score.Explain()
                )).ToList();

            sections.Add(new ReportItem($"{title} ({results.Count})", children: items, isExpanded: !changed));
        }

        /// <summary>
        /// <c>A.B.C -> X.Y.C [Likely 85]</c>, with the reason a text has been left.
        /// </summary>
        public static string DescribeTextReference(TextReferenceResult result)
        {
            var text = $"{result.Match.MatchedText} -> {result.Match.ReplacementText} [{result.Match.Level} {result.Match.Score.Value}]";

            return result.Reason == null
                ? text
                : $"{text}: {result.Reason}";
        }

        private static void AddAdjusted(List<ReportItem> sections, AdjustReport report)
        {
            var files = report.FilesWith(AdjustFileStatus.Adjusted).ToList();
            if (files.Count == 0)
            {
                return;
            }

            var items = files.Select(file => new ReportItem(
                report.DisplayPath(file.FilePath),
                DescribeAdjusted(file),
                file.FilePath,
                children: file.EditedFiles.Select(e => ToItem(report, e)).ToList()
                )).ToList();

            sections.Add(new ReportItem($"Adjusted ({files.Count})", children: items, isExpanded: true));
        }

        private static string DescribeAdjusted(AdjustFileOutcome file)
        {
            var parts = new List<string>();

            if (file.Changes.Count > 0)
            {
                parts.Add(string.Join(", ", file.Changes.Select(c => c.ToString())));
            }

            parts.Add($"edits in other files: {file.EditsInOtherFiles}");

            return string.Join("; ", parts);
        }

        private static void AddGroup(
            List<ReportItem> sections,
            AdjustReport report,
            AdjustFileStatus status,
            string title,
            bool withMessage
            )
        {
            var files = report.FilesWith(status).ToList();
            if (files.Count == 0)
            {
                return;
            }

            var items = files.Select(file => new ReportItem(
                report.DisplayPath(file.FilePath),
                withMessage ? file.Message : null,
                file.FilePath
                )).ToList();

            sections.Add(new ReportItem($"{title} ({files.Count})", children: items, isExpanded: true));
        }

        private static void AddCleanup(List<ReportItem> sections, AdjustReport report)
        {
            if (report.CleanupFiles.Count == 0)
            {
                return;
            }

            sections.Add(
                new ReportItem(
                    $"Cleanup ({report.CleanupFiles.Count})",
                    children: report.CleanupFiles.Select(f => ToItem(report, f)).ToList(),
                    isExpanded: false
                    )
                );
        }

        private static ReportItem ToItem(AdjustReport report, AdjustTouchedFile file)
        {
            return new ReportItem(
                report.DisplayPath(file.FilePath),
                file.DescribeEdits(),
                file.FilePath
                );
        }
    }
}
