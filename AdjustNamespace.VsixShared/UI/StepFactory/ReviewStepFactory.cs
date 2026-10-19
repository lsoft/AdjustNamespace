using AdjustNamespace.UI.Control;
using AdjustNamespace.UI.ViewModel;
using AdjustNamespace.UI.ViewModel.Review;
using System;

namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// Factory of the review step, the one between the selection and the run
    /// (<see cref="ReviewStepViewModel"/>).
    ///
    /// The step is shown only when the search of the text references has found something to
    /// show: otherwise there is nothing to review and the factory goes straight to the run.
    /// </summary>
    public class ReviewStepFactory : IStepFactory<ReviewParameters>
    {
        private readonly AdjustContext _context;
        private readonly IWizardHost _host;

        /// <summary>
        /// Factory of the previous step (this step allows to go back), asked for at the moment
        /// the step is created, see <see cref="WizardChain"/>.
        /// </summary>
        private readonly Func<IStepFactory<SelectedStepParameters>> _previousStepFactory;

        private readonly IStepFactory<PerformingParameters> _nextStepFactory;

        public ReviewStepFactory(
            AdjustContext context,
            IWizardHost host,
            Func<IStepFactory<SelectedStepParameters>> previousStepFactory,
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
        public async System.Threading.Tasks.Task CreateAsync(ReviewParameters parameters)
        {
            if (!parameters.TextReferences.HasAnythingToShow)
            {
                await _nextStepFactory.CreateAsync(
                    new PerformingParameters(
                        parameters.SubjectFilePaths,
                        parameters.ReplaceRegex,
                        parameters.Blocked,
                        parameters.TextReferences
                        )
                    );

                return;
            }

            await _host.ShowAsync(
                new ReviewUserControl(),
                new ReviewStepViewModel(
                    _context,
                    _host,
                    _previousStepFactory,
                    _nextStepFactory,
                    parameters
                    )
                );
        }
    }
}
