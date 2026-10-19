using AdjustNamespace.UI.Control;
using AdjustNamespace.UI.ViewModel;
using AdjustNamespace.UI.ViewModel.Reporting;
using System;

namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// Factory of the last wizard step: the report of the run (<see cref="ReportStepViewModel"/>).
    /// </summary>
    public class ReportStepFactory : IStepFactory<ReportParameters>
    {
        private readonly IWizardHost _host;

        public ReportStepFactory(
            IWizardHost host
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            _host = host;
        }

        /// <inheritdoc/>
        public async System.Threading.Tasks.Task CreateAsync(ReportParameters parameters)
        {
            await _host.ShowAsync(
                new ReportUserControl(),
                new ReportStepViewModel(
                    _host,
                    parameters
                    )
                );
        }
    }
}
