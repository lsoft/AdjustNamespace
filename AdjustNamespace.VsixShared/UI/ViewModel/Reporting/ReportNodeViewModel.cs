using AdjustNamespace.Adjusting.Report;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.UI.ViewModel.Reporting
{
    /// <summary>
    /// A node of the tree the report step shows: a thin wrapper over a <see cref="ReportItem"/>
    /// of the core, which adds the one thing the tree needs from a viewmodel, the expanded state.
    /// </summary>
    public sealed class ReportNodeViewModel : BaseViewModel
    {
        private bool _isExpanded;

        /// <summary>
        /// The main text of the node.
        /// </summary>
        public string Title
        {
            get;
        }

        /// <summary>
        /// The secondary text of the node, or an empty string.
        /// </summary>
        public string Detail
        {
            get;
        }

        /// <summary>
        /// The tooltip: the full path of the file, or the detail of a node with no file.
        /// </summary>
        public string ToolTip
        {
            get;
        }

        /// <summary>
        /// The file the node stands for, or <c>null</c>.
        /// </summary>
        public string? FilePath
        {
            get;
        }

        /// <summary>
        /// The line of the file to open, starting with 1; 0 if there is none.
        /// </summary>
        public int Line
        {
            get;
        }

        /// <summary>
        /// The nodes below this one.
        /// </summary>
        public IReadOnlyList<ReportNodeViewModel> Children
        {
            get;
        }

        /// <summary>
        /// The node is shown expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public ReportNodeViewModel(
            ReportItem item
            )
        {
            if (item is null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Title = item.Title;
            Detail = item.Detail ?? string.Empty;
            ToolTip = item.ToolTip ?? item.FilePath ?? (string.IsNullOrEmpty(item.Detail) ? item.Title : item.Detail!);
            FilePath = item.FilePath;
            Line = item.Line;
            Children = item.Children.Select(c => new ReportNodeViewModel(c)).ToList();
            _isExpanded = item.IsExpanded;
        }
    }
}
