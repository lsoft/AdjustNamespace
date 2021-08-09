using Task = System.Threading.Tasks.Task;

namespace AdjustNamespace.UI.StepFactory
{
    public interface IStepFactory
    {
        Task CreateAsync(object argument);
    }
}
