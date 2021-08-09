namespace AdjustNamespace.Helper
{
    public abstract class ChainViewModel : BaseViewModel
    {
        protected ChainViewModel()
            : base()
        {
        }

        public abstract System.Threading.Tasks.Task StartAsync();
    }

}
