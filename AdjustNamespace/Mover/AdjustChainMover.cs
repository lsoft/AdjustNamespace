using AdjustNamespace.Control;
using AdjustNamespace.Helper;
using AdjustNamespace.ViewModel;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AdjustNamespace.Mover
{
    public class AdjustChainMover : IChainMoverState
    {
        private enum StepEnum
        {
            NotSet,
            Preparation,
            Performing
        }

        private readonly List<string> _filePaths;

        private StepEnum _currentStep = StepEnum.NotSet;

        public bool BlockMovingForward
        {
            get;
            set;
        }
        public IAsyncServiceProvider ServiceProvider
        {
            get;
        }

        public AdjustChainMover(
            IAsyncServiceProvider serviceProvider,
            List<string> filePaths
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (filePaths is null)
            {
                throw new ArgumentNullException(nameof(filePaths));
            }
            ServiceProvider = serviceProvider;
            _filePaths = filePaths;
        }

        internal bool TryToMove(Dispatcher dispatcher, out ChainViewModel? vm, out UserControl? uc)
        {
            if (BlockMovingForward)
            {
                vm = null;
                uc = null;
                return false;
            }


            switch (_currentStep)
            {
                case StepEnum.NotSet:
                    vm = new PreparationStepViewModel(this, dispatcher, _filePaths);
                    uc = new PreparationUserControl();
                    _currentStep = StepEnum.Preparation;
                    return true;
                case StepEnum.Preparation:
                    vm = new PerformingViewModel(this, dispatcher, _filePaths);
                    uc = new PerformingUserControl();
                    _currentStep = StepEnum.Performing;
                    return true;
                case StepEnum.Performing:
                    vm = null;
                    uc = null;
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_currentStep));
            }
        }
    }
}
