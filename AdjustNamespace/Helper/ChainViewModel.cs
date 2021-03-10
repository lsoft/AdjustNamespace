using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AdjustNamespace.Helper
{
    public abstract class ChainViewModel : BaseViewModel
    {
        protected ChainViewModel(Dispatcher dispatcher)
            : base(dispatcher)
        {
        }

        public abstract System.Threading.Tasks.Task StartAsync();
    }

}
