using AdjustNamespace.UI.ViewModel;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Windows.Controls;

namespace AdjustNamespace.UI
{
    /// <summary>
    /// The content area of the wizard window: the steps replace each other in it.
    /// </summary>
    public sealed class WizardHost : IWizardHost
    {
        private readonly DialogWindow _window;
        private readonly ContentControl _targetControl;

        /// <param name="window">The wizard window.</param>
        /// <param name="targetControl">The control of the window the steps are shown in.</param>
        public WizardHost(
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

            _window = window;
            _targetControl = targetControl;
        }

        /// <inheritdoc/>
        public async System.Threading.Tasks.Task ShowAsync(UserControl view, ChainViewModel viewModel)
        {
            if (view is null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (viewModel is null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            view.DataContext = viewModel;
            _targetControl.Content = view;

            try
            {
                await viewModel.StartAsync();
            }
            catch (Exception excp)
            {
                //a step which has died takes the wizard nowhere, so the user is shown what
                //has happened instead of an empty window
                _targetControl.Content = excp.Message + Environment.NewLine + excp.StackTrace;
                _targetControl.Foreground = System.Windows.Media.Brushes.Red;

                Logging.LogVS(excp);
            }
        }

        /// <inheritdoc/>
        public void Close()
        {
            _window.Close();
        }
    }
}
