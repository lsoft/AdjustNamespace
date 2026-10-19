using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// What of a report goes to the Error List, as a warning each: the text references the run
    /// has left (at the line of the name) and the compilation errors the run has introduced. The
    /// decision is made here, in the core, so that the code which talks to Visual Studio has
    /// nothing to decide.
    /// </summary>
    public static class AdjustReportErrorList
    {
        public const string Prefix = "Adjust Namespaces: ";

        public static IReadOnlyList<ErrorListEntry> Build(AdjustReport report)
        {
            if (report is null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var result = new List<ErrorListEntry>();

            foreach (var left in report.TextReferences.Where(t => !t.IsChanged))
            {
                var match = left.Match;

                result.Add(
                    new ErrorListEntry(
                        $"{Prefix}the text reference {match.MatchedText} was not changed to {match.ReplacementText} "
                        + $"({match.Level}, {match.Score.Value}; {left.Reason})",
                        match.FilePath,
                        match.Line,
                        match.Column
                        )
                    );
            }

            if (report.BuildCheck != null)
            {
                foreach (var error in report.BuildCheck.NewErrors)
                {
                    result.Add(
                        new ErrorListEntry(
                            $"{Prefix}new compilation error after the run: {error.Id} {error.Message}",
                            error.FilePath,
                            error.Line,
                            error.Column
                            )
                        );
                }
            }

            return result;
        }
    }
}
