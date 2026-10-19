using AdjustNamespace.UI.Control;
using System;
using AdjustNamespace.UI.ViewModel;

namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// Factory of the third (last) wizard step: the adjusting itself
    /// (<see cref="PerformingViewModel"/>).
    /// </summary>
    public class PerformingStepFactory : IStepFactory<PerformingParameters>
    {
        private readonly AdjustContext _context;
        private readonly IWizardHost _host;
        private readonly IStepFactory<ReportParameters> _nextStepFactory;

        public PerformingStepFactory(
            AdjustContext context,
            IWizardHost host,
            IStepFactory<ReportParameters> nextStepFactory
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            _context = context;
            _host = host;
            _nextStepFactory = nextStepFactory;
        }

        /// <inheritdoc/>
        public async System.Threading.Tasks.Task CreateAsync(PerformingParameters parameters)
        {
            await _host.ShowAsync(
                new PerformingUserControl(),
                new PerformingViewModel(
                    _context,
                    _host,
                    _nextStepFactory,
                    parameters
                    )
                );
        }

    }
}
