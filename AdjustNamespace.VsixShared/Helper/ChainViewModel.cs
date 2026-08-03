namespace AdjustNamespace.Helper
{
    /// <summary>
    /// Base class of the viewmodels of the wizard steps.
    /// The steps are chained: every step knows the factory of the next one
    /// (see <c>AdjustNamespace.UI.StepFactory</c> namespace) and switches
    /// the content of the wizard window to it.
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
