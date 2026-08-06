using AdjustNamespace.UI.Control;
using AdjustNamespace.UI.ViewModel;
using AdjustNamespace.UI.ViewModel.Select;
using System;

namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// Factory of the second wizard step: the file selection and the target namespace regex
    /// (<see cref="SelectedStepViewModel"/>).
    /// </summary>
    public class SelectedStepFactory : IStepFactory<SelectedStepParameters>
    {
        private readonly AdjustContext _context;
        private readonly IWizardHost _host;

        /// <summary>
        /// Factory of the previous step (this step allows to go back).
        /// It is asked for at the moment the step is created and not at the moment this
        /// factory is: the first two steps reference each other, so one of them is
        /// necessarily built after the other, see <see cref="WizardChain"/>.
        /// </summary>
        private readonly Func<IStepFactory<PreparationParameters>> _previousStepFactory;

        private readonly IStepFactory<PerformingParameters> _nextStepFactory;

        public SelectedStepFactory(
            AdjustContext context,
            IWizardHost host,
            Func<IStepFactory<PreparationParameters>> previousStepFactory,
            IStepFactory<PerformingParameters> nextStepFactory
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (previousStepFactory is null)
            {
                throw new ArgumentNullException(nameof(previousStepFactory));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            _context = context;
            _host = host;
            _previousStepFactory = previousStepFactory;
            _nextStepFactory = nextStepFactory;
        }

        /// <inheritdoc/>
        public async System.Threading.Tasks.Task CreateAsync(SelectedStepParameters parameters)
        {
            await _host.ShowAsync(
                new SelectedUserControl(),
                new SelectedStepViewModel(
                    _context,
                    _host,
                    _previousStepFactory(),
                    _nextStepFactory,
                    parameters
                    )
                );
        }
    }
}
