using AdjustNamespace.UI.ViewModel;
using System;

namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// The steps of the wizard, wired to each other: preparation -> selection -> performing,
    /// and back from the selection to the preparation.
    ///
    /// The chain is built here and nowhere else, because it is not a line: the second step
    /// allows to go back, so the first two steps reference each other and one of them is
    /// necessarily built after the other.
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

            var performing = new PerformingStepFactory(
                context,
                host
                );

            var selection = new SelectedStepFactory(
                context,
                host,
                //the preparation step does not exist yet, and it is not needed
                //until the user presses `Back` on the selection step
                GetPreparation,
                performing
                );

            Preparation = new PreparationStepFactory(
                context,
                host,
                selection
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
    }
}
