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

        private void ListView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space)
            {
                e.Handled = true;
            }
        }
    }
}
