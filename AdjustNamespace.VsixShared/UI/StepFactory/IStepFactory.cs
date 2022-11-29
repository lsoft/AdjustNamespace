namespace AdjustNamespace.UI.StepFactory
{
    public interface IStepFactory
    {
        System.Threading.Tasks.Task CreateAsync(object argument);
    }
}
