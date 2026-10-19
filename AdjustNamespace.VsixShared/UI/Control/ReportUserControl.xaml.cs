using AdjustNamespace.UI.ViewModel.Reporting;
using System.Windows.Controls;
using System.Windows.Input;

namespace AdjustNamespace.UI.Control
{
    /// <summary>
    /// Interaction logic for ReportUserControl.xaml
    /// </summary>
    public partial class ReportUserControl : UserControl
    {
        public ReportUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// A double click on a node opens its file. The event is raised for the node under the
        /// mouse and then for every node above it, so only the selected one answers.
        /// </summary>
        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TreeViewItem item || !item.IsSelected)
            {
                return;
            }

            if (item.DataContext is ReportNodeViewModel node
                && DataContext is ReportStepViewModel viewModel
                && viewModel.OpenNodeCommand.CanExecute(node))
            {
                viewModel.OpenNodeCommand.Execute(node);

                e.Handled = true;
            }
        }
    }
}
