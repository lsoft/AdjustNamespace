using System.Windows.Controls;

namespace AdjustNamespace.UI.Control
{
    /// <summary>
    /// Interaction logic for SelectedUserControl.xaml
    /// </summary>
    public partial class SelectedUserControl : UserControl
    {
        public SelectedUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Suppress the default reaction of the list on the Space key:
        /// the key is handled by the viewmodel command instead.
        /// </summary>
        private void ListView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space)
            {
                e.Handled = true;
            }
        }
    }
}
