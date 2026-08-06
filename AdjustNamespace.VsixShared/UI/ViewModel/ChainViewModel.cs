namespace AdjustNamespace.UI.ViewModel
{
    /// <summary>
    /// Base class of the viewmodels of the wizard steps.
    /// The steps are chained: every step knows the factory of the next one and the parameters
    /// it hands over (see <c>AdjustNamespace.UI.StepFactory</c> namespace), and the factory
    /// asks <c>AdjustNamespace.UI.IWizardHost</c> to replace the content of the window.
    /// </summary>
    public abstract class ChainViewModel : BaseViewModel
    {
        protected ChainViewModel()
            : base()
        {
        }

        /// <summary>
        /// Start the work of this step. Called right after the step has been shown.
        /// </summary>
        public abstract System.Threading.Tasks.Task StartAsync();
    }

}
