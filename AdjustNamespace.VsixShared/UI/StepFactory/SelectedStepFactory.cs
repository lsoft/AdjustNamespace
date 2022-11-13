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
        private readonly VsServices _vss;
        private readonly ContentControl _targetControl;
        private readonly IStepFactory _nextStepFactory;

        public SelectedStepFactory(
            VsServices vss,
            ContentControl targetControl,
            IStepFactory nextStepFactory
            )
        {
            if (targetControl is null)
            {
                throw new ArgumentNullException(nameof(targetControl));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            _vss = vss;
            _targetControl = targetControl;
            _nextStepFactory = nextStepFactory;
        }

        public async Task CreateAsync(object argument)
        {
            var v = new SelectedUserControl();

            var vm  = new SelectedStepViewModel(
                _vss,
                _nextStepFactory,
                (List<FileEx>)argument
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
