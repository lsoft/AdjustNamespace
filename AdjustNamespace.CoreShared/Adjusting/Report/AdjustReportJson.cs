using AdjustNamespace.Adjusting.Edit;
using System;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// The JSON renderer of an <see cref="AdjustReport"/>. The paths are shown the way the
    /// Markdown shows them, relative to the folder of the solution.
    /// </summary>
    public static class AdjustReportJson
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

            var json = new ReportJsonWriter();

            json.BeginObject();

            json.Property("outcome", report.Outcome.ToString());
            json.Property("failure", report.FailureMessage);
            json.Property("solutionFolder", report.SolutionFolder);

            json.Name("summary");
            json.BeginObject();
            json.Property("adjusted", report.AdjustedCount);
            json.Property("blocked", report.BlockedCount);
            json.Property("skipped", report.SkippedCount);
            json.Property("notProcessed", report.NotProcessedCount);
            json.Property("failed", report.FailedCount);
            json.EndObject();

            json.Name("files");
            json.BeginArray();
            foreach (var file in report.Files)
            {
                json.BeginObject();
                json.Property("path", report.DisplayPath(file.FilePath));
                json.Property("status", file.Status.ToString());

                json.Name("namespaces");
                json.BeginArray();
                foreach (var change in file.Changes)
                {
                    json.BeginObject();
                    json.Property("from", change.Old);
                    json.Property("to", change.New);
                    json.EndObject();
                }

                json.EndArray();

                json.Property("editsInOtherFiles", file.EditsInOtherFiles);
                json.Property("blockKind", file.Block?.Kind.ToString());
                json.Property("reason", file.Message);

                WriteTouchedFiles(json, report, "editedFiles", file.EditedFiles);
                json.EndObject();
            }

            json.EndArray();

            WriteTouchedFiles(json, report, "cleanupFiles", report.CleanupFiles);
            WriteTouchedFiles(json, report, "touchedFiles", report.TouchedFiles);

            WriteTextReferences(json, report);

            WriteBuildCheck(json, report);

            json.Name("notes");
            json.BeginArray();
            foreach (var note in report.Notes)
            {
                json.Value(note);
            }

            json.EndArray();

            json.EndObject();

            return json.ToString() + "\n";
        }

        private static void WriteBuildCheck(
            ReportJsonWriter json,
            AdjustReport report
            )
        {
            json.Name("buildCheck");

            var check = report.BuildCheck;
            if (check == null)
            {
                json.Value((string?)null);
                return;
            }

            json.BeginObject();
            json.Property("errorsBefore", check.ErrorsBefore);
            json.Property("errorsAfter", check.ErrorsAfter);

            json.Name("projects");
            json.BeginArray();
            foreach (var name in check.ProjectNames)
            {
                json.Value(name);
            }

            json.EndArray();

            json.Name("newErrors");
            json.BeginArray();
            foreach (var error in check.NewErrors)
            {
                json.BeginObject();
                json.Property("project", error.ProjectName);
                json.Property("id", error.Id);
                json.Property("message", error.Message);
                json.Property("path", error.FilePath == null ? null : report.DisplayPath(error.FilePath));
                json.Property("line", error.Line);
                json.EndObject();
            }

            json.EndArray();
            json.EndObject();
        }

        private static void WriteTextReferences(
            ReportJsonWriter json,
            AdjustReport report
            )
        {
            json.Name("textReferences");
            json.BeginObject();

            json.Property("searched", report.TextReferencesWereSearched);
            json.Property("changed", report.TextChangedCount);
            json.Property("left", report.TextLeftCount);
            json.Property("hiddenUnlikely", report.HiddenTextReferencesCount);

            json.Name("items");
            json.BeginArray();
            foreach (var result in report.TextReferences)
            {
                var match = result.Match;

                json.BeginObject();
                json.Property("path", report.DisplayPath(match.FilePath));
                json.Property("line", match.Line);
                json.Property("column", match.Column);
                json.Property("file", match.FileKind.ToString());
                json.Property("place", match.Place.ToString());
                json.Property("text", match.MatchedText);
                json.Property("replacement", match.ReplacementText);
                json.Property("score", match.Score.Value);
                json.Property("level", match.Level.ToString());
                json.Property("changed", result.IsChanged);
                json.Property("reason", result.Reason);

                json.Name("signals");
                json.BeginArray();
                foreach (var signal in match.Score.Signals)
                {
                    json.BeginObject();
                    json.Property("name", signal.Name);
                    json.Property("weight", signal.Weight);
                    json.EndObject();
                }

                json.EndArray();
                json.EndObject();
            }

            json.EndArray();
            json.EndObject();
        }

        private static void WriteTouchedFiles(
            ReportJsonWriter json,
            AdjustReport report,
            string name,
            System.Collections.Generic.IReadOnlyList<AdjustTouchedFile> files
            )
        {
            json.Name(name);
            json.BeginArray();
            foreach (var file in files)
            {
                json.BeginObject();
                json.Property("path", report.DisplayPath(file.FilePath));

                json.Name("edits");
                json.BeginObject();
                foreach (var edit in file.Edits)
                {
                    json.Property(edit.Key.ToString(), edit.Value);
                }

                json.EndObject();
                json.EndObject();
            }

            json.EndArray();
        }
    }
}
