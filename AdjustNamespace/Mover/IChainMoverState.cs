using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;

namespace AdjustNamespace.Mover
{
    public interface IChainMoverState
    {
        bool BlockMovingForward
        {
            get;
            set;
        }

        IAsyncServiceProvider ServiceProvider
        {
            get;
        }
    }
}
