using AdjustNamespace.Adjusting.Report;
using Microsoft.VisualStudio.Shell;
using System;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// Puts what a run has not fixed into the Error List of Visual Studio as warnings, so it
    /// stays visible after the window of the wizard is closed: the text references the run has
    /// left and the compilation errors it has introduced (the build check).
    ///
    /// What goes there is decided by the core (<see cref="AdjustReportErrorList"/>); this class
    /// only talks to the shell. The entries of the previous run are replaced.
    /// </summary>
    public static class ErrorListReporter
    {
        private static ErrorListProvider? _provider;

        /// <summary>
        /// Replace the entries of the previous run with the ones of this report. Must be called
        /// on the main thread.
        /// </summary>
        public static void Show(AdjustReport report)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (report is null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var items = AdjustReportErrorList.Build(report);

            if (_provider is null && items.Count == 0)
            {
                return;
            }

            var provider = _provider ??= CreateProvider();

            provider.Tasks.Clear();

            foreach (var item in items)
            {
                provider.Tasks.Add(CreateTask(provider, item));
            }

            if (items.Count > 0)
            {
                provider.Show();
            }
        }

        private static ErrorListProvider CreateProvider()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return new ErrorListProvider(ServiceProvider.GlobalProvider)
            {
                ProviderName = "Adjust Namespaces",
                ProviderGuid = new Guid("5d0a0c7f-8a3c-4b53-9c51-3b2f1f0d6a11")
            };
        }

        private static ErrorTask CreateTask(ErrorListProvider provider, ErrorListEntry item)
        {
            var task = new ErrorTask
            {
                Text = item.Text,
                Category = TaskCategory.User,
                ErrorCategory = TaskErrorCategory.Warning,
                Document = item.FilePath ?? string.Empty,
                //the Error List counts the lines and the columns from 0
                Line = Math.Max(0, item.Line - 1),
                Column = Math.Max(0, item.Column)
            };

            task.Navigate += (sender, e) =>
            {
                if (string.IsNullOrEmpty(task.Document))
                {
                    return;
                }

                //ErrorListProvider.Navigate expects the line to be one based
                task.Line++;
                try
                {
                    provider.Navigate(task, Guid.Empty);
                }
                finally
                {
                    task.Line--;
                }
            };

            return task;
        }
    }
}
