using AdjustNamespace.UI.ViewModel;
using System.Windows.Controls;

namespace AdjustNamespace.UI
{
    /// <summary>
    /// The wizard window as a step sees it: a place to be shown in and a way to close it.
    ///
    /// The steps do not know the window itself, so neither a step factory has to be given
    /// a <c>DialogWindow</c> nor the `Close` button of a step has to be handed one by
    /// a xaml binding.
    /// </summary>
    public interface IWizardHost
    {
        /// <summary>
        /// Show the step in the wizard and start its work.
        /// </summary>
        /// <param name="view">Control of the step.</param>
        /// <param name="viewModel">Viewmodel of the step.</param>
        System.Threading.Tasks.Task ShowAsync(UserControl view, ChainViewModel viewModel);

        /// <summary>
        /// Close the wizard.
        /// </summary>
        void Close();
    }
}
