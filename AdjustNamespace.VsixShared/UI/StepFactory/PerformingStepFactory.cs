using AdjustNamespace.UI.Control;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;
using AdjustNamespace.UI.ViewModel;
using Microsoft.VisualStudio.PlatformUI;

namespace AdjustNamespace.UI.StepFactory
{
    public class PerformingStepFactory : IStepFactory
    {
        private readonly IAsyncServiceProvider _serviceProvider;
        private readonly DialogWindow _window;
        private readonly ContentControl _targetControl;

        public PerformingStepFactory(
            IAsyncServiceProvider serviceProvider,
            DialogWindow window,
            ContentControl targetControl
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (targetControl is null)
            {
                throw new ArgumentNullException(nameof(targetControl));
            }

            _serviceProvider = serviceProvider;
            _window = window;
            _targetControl = targetControl;
        }

        public async Task CreateAsync(object argument)
        {
            var v = new PerformingUserControl();

            var vm = new PerformingViewModel(
                _serviceProvider,
                () => _window.Close(),
                (List<string>)argument
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

