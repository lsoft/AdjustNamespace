using AdjustNamespace.UI.Control;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using AdjustNamespace.UI.ViewModel;

namespace AdjustNamespace.UI.StepFactory
{
    /// <summary>
    /// Factory of the second wizard step: the file selection and the target namespace regex
    /// (<see cref="SelectedStepViewModel"/>).
    /// </summary>
    public class SelectedStepFactory : IStepFactory
    {
        private readonly VsServices _vss;
        private readonly ContentControl _targetControl;
        private readonly IStepFactory _nextStepFactory;

        /// <summary>
        /// Factory of the previous step (this step allows to go back).
        /// It is a property (not a constructor parameter) because the steps reference
        /// each other and hence cannot be constructed in a single pass.
        /// </summary>
        public PreparationStepFactory? PreviousStepFactory
        {
            get;
            set;
        }

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

        /// <inheritdoc/>
        /// <param name="argument">A <see cref="SelectedStepParameters"/> instance.</param>
        public async System.Threading.Tasks.Task CreateAsync(object argument)
        {
            var a = (SelectedStepParameters)argument;

            var v = new SelectedUserControl();

            var vm = new SelectedStepViewModel(
                _vss,
                PreviousStepFactory!,
                _nextStepFactory,
                a
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
