using AdjustNamespace.UI.ViewModel;
using System;

namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// The steps of the wizard, wired to each other: preparation -> selection -> review ->
    /// performing -> report, and back from the selection to the preparation and from the review
    /// to the selection.
    ///
    /// The chain is built here and nowhere else, because it is not a line: the second step
    /// allows to go back, so the first two steps reference each other and one of them is
    /// necessarily built after the other (the review step goes back too).
    /// </summary>
    public sealed class WizardChain
    {
        /// <summary>
        /// The step the wizard starts with.
        /// </summary>
        private IStepFactory<PreparationParameters> Preparation
        {
            get;
        }

        /// <summary>
        /// The selection step: the review step goes back to it.
        /// </summary>
        private IStepFactory<SelectedStepParameters> Selection
        {
            get;
        }

        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="host">The window the steps are shown in.</param>
        public WizardChain(
            AdjustContext context,
            IWizardHost host
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            var report = new ReportStepFactory(
                host
                );

            var performing = new PerformingStepFactory(
                context,
                host,
                report
                );

            var review = new ReviewStepFactory(
                context,
                host,
                //the selection step does not exist yet, and it is not needed
                //until the user presses `Previous` on the review step
                GetSelection,
                performing
                );

            Selection = new SelectedStepFactory(
                context,
                host,
                //the preparation step does not exist yet, and it is not needed
                //until the user presses `Back` on the selection step
                GetPreparation,
                review
                );

            Preparation = new PreparationStepFactory(
                context,
                host,
                Selection
                );
        }

        /// <summary>
        /// Show the first step of the wizard.
        /// </summary>
        /// <param name="parameters">Full paths of the files chosen by the user.</param>
        public async System.Threading.Tasks.Task StartAsync(PreparationParameters parameters)
        {
            await Preparation.CreateAsync(parameters);
        }

        /// <summary>
        /// The first step as the second one asks for it, i.e. after the chain has been built.
        /// </summary>
        private IStepFactory<PreparationParameters> GetPreparation()
        {
            return Preparation;
        }

        /// <summary>
        /// The second step as the review asks for it, i.e. after the chain has been built.
        /// </summary>
        private IStepFactory<SelectedStepParameters> GetSelection()
        {
            return Selection;
        }
    }
}
