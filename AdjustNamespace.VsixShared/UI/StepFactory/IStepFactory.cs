namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// Factory of a wizard step. It creates the control and the viewmodel of the step,
    /// places the control into the wizard window and starts the work of the step.
    /// </summary>
    public interface IStepFactory
    {
        /// <summary>
        /// Create and show the step.
        /// </summary>
        /// <param name="argument">Parameters of the step, produced by the previous step.</param>
        System.Threading.Tasks.Task CreateAsync(object argument);
    }
}
