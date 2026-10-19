using AdjustNamespace.UI.ViewModel.Review;
using System.Windows.Controls;
using System.Windows.Input;

namespace AdjustNamespace.UI.Control
{
    /// <summary>
    /// Interaction logic for ReviewUserControl.xaml
    /// </summary>
    public partial class ReviewUserControl : UserControl
    {
        public ReviewUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// A double click on a text reference opens its file at its line. The event is raised for
        /// the node under the mouse and then for every node above it, so only the selected one
        /// answers.
        /// </summary>
        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not TreeViewItem item || !item.IsSelected)
            {
                return;
            }

            if (item.DataContext is ReviewMatchViewModel row
                && DataContext is ReviewStepViewModel viewModel
                && viewModel.OpenCommand.CanExecute(row))
            {
                viewModel.OpenCommand.Execute(row);

                e.Handled = true;
            }
        }
    }
}
