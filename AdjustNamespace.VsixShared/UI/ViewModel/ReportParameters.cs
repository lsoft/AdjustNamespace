using AdjustNamespace.Adjusting.Report;

namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Parameters of the report step, the one after the run.
    /// </summary>
    public readonly struct ReportParameters
    {
        /// <summary>
        /// What the run has done.
        /// </summary>
        public readonly AdjustReport Report;

        /// <summary>
        /// The changes of the run have been rolled back (it has failed), so the report tells
        /// what the run was going to do and not what the solution looks like now.
        /// </summary>
        public readonly bool RolledBack;

        public ReportParameters(
            AdjustReport report,
            bool rolledBack
            )
        {
            if (report is null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            Report = report;
            RolledBack = rolledBack;
        }
    }
}
