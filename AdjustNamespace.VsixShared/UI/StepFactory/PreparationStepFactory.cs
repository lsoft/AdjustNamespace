using AdjustNamespace.UI.Control;
using System;
using AdjustNamespace.UI.ViewModel;

namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// Factory of the first wizard step: the solution compilation check
    /// (<see cref="PreparationStepViewModel"/>).
    /// </summary>
    public class PreparationStepFactory : IStepFactory<PreparationParameters>
    {
        private readonly AdjustContext _context;
        private readonly IWizardHost _host;
        private readonly IStepFactory<SelectedStepParameters> _nextStepFactory;

        public PreparationStepFactory(
            AdjustContext context,
            IWizardHost host,
            IStepFactory<SelectedStepParameters> nextStepFactory
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
        public async System.Threading.Tasks.Task CreateAsync(PreparationParameters parameters)
        {
            await _host.ShowAsync(
                new PreparationUserControl(),
                new PreparationStepViewModel(
                    _context,
                    _host,
                    _nextStepFactory,
                    parameters
                    )
                );
        }

    }
}
