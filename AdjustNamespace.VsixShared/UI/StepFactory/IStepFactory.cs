namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// Factory of a wizard step. It creates the control and the viewmodel of the step
    /// and hands them to the <see cref="IWizardHost"/>, which shows them and starts the work.
    ///
    /// A step names what it has to be entered with, so a step which is wired to the wrong
    /// neighbour does not compile at all instead of throwing an
    /// <see cref="System.InvalidCastException"/> in the middle of the wizard.
    /// </summary>
    /// <typeparam name="TParameters">
    /// What this step needs to be entered with. It is produced by the step which moves here.
    /// </typeparam>
    public interface IStepFactory<in TParameters>
    {
        /// <summary>
        /// Create and show the step.
        /// </summary>
        /// <param name="parameters">Parameters of the step, produced by the previous step.</param>
        System.Threading.Tasks.Task CreateAsync(TParameters parameters);
    }
}
