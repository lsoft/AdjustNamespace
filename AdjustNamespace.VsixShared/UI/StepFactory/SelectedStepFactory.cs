using AdjustNamespace.UI.Control;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;
using AdjustNamespace.UI.ViewModel;

namespace AdjustNamespace.UI.StepFactory
{
    public class SelectedStepFactory : IStepFactory
    {
        private readonly IAsyncServiceProvider _serviceProvider;
        private readonly ContentControl _targetControl;
        private readonly IStepFactory _nextStepFactory;

        public SelectedStepFactory(
            IAsyncServiceProvider serviceProvider,
            ContentControl targetControl,
            IStepFactory nextStepFactory
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (targetControl is null)
            {
                throw new ArgumentNullException(nameof(targetControl));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            _serviceProvider = serviceProvider;
            _targetControl = targetControl;
            _nextStepFactory = nextStepFactory;
        }

        public async Task CreateAsync(object argument)
        {
            var v = new SelectedUserControl();

            var vm  = new SelectedStepViewModel(
                _serviceProvider,
                _nextStepFactory,
                (List<FileExtension>)argument
                );

            v.DataContext = vm;
            _targetControl.Content = v;

            try
            {
                await vm!.StartAsync();
            }
            catch (Exception excp)
            {
                _targetControl.Content = excp.Message + Environment.NewLine + excp.StackTrace;
                _targetControl.Foreground = System.Windows.Media.Brushes.Red;
                Logging.LogVS(excp);
            }
        }

    }
}
