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
        private readonly VsServices _vss;
        private readonly DialogWindow _window;
        private readonly ContentControl _targetControl;

        public PerformingStepFactory(
            VsServices vss,
            DialogWindow window,
            ContentControl targetControl
            )
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (targetControl is null)
            {
                throw new ArgumentNullException(nameof(targetControl));
            }

            _vss = vss;
            _window = window;
            _targetControl = targetControl;
        }

        public async Task CreateAsync(object argument)
        {
            var v = new PerformingUserControl();

            var vm = new PerformingViewModel(
                _vss,
                () => _window.Close(),
                (PerformingParameters)argument
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

