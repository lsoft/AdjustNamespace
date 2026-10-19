using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// A node of the tree the wizard shows a report as: a section (<c>Adjusted (3)</c>), a file
    /// of it, the files that file has changed.
    ///
    /// The tree is built by <see cref="AdjustReportSections"/> in the core, so the viewmodel of the
    /// wizard only wraps it and has nothing to decide.
    /// </summary>
    [DebuggerDisplay("{Title}")]
    public sealed class ReportItem
    {
        private static readonly IReadOnlyList<ReportItem> NoChildren = Array.Empty<ReportItem>();

        /// <summary>
        /// The main text of the node.
        /// </summary>
        public string Title
        {
            get;
        }

        /// <summary>
        /// The secondary text of the node (the namespaces, the reason), or <c>null</c>.
        /// </summary>
        public string? Detail
        {
            get;
        }

        /// <summary>
        /// The file the node stands for (a double click opens it), or <c>null</c>.
        /// </summary>
        public string? FilePath
        {
            get;
        }

        /// <summary>
        /// The line of the file to open, starting with 1; 0 if the node has none.
        /// </summary>
        public int Line
        {
            get;
        }

        /// <summary>
        /// The nodes below this one.
        /// </summary>
        public IReadOnlyList<ReportItem> Children
        {
            get;
        }

        /// <summary>
        /// The node is shown expanded.
        /// </summary>
        public bool IsExpanded
        {
            get;
        }

        /// <summary>
        /// The tooltip of the node (the reason of the score of a text reference), or <c>null</c>.
        /// </summary>
        public string? ToolTip
        {
            get;
        }

        public ReportItem(
            string title,
            string? detail = null,
            string? filePath = null,
            int line = 0,
            IReadOnlyList<ReportItem>? children = null,
            bool isExpanded = false,
            string? toolTip = null
            )
        {
            if (title is null)
            {
                throw new ArgumentNullException(nameof(title));
            }

            Title = title;
            Detail = detail;
            FilePath = filePath;
            Line = line;
            Children = children ?? NoChildren;
            IsExpanded = isExpanded;
            ToolTip = toolTip;
        }
    }
}
