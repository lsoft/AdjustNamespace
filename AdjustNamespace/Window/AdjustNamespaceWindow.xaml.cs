using AdjustNamespace.Mover;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AdjustNamespace.Window
{
    /// <summary>
    /// Interaction logic for AdjustNamespaceWindow.xaml
    /// </summary>
    public partial class AdjustNamespaceWindow : DialogWindow
    {
        private readonly AdjustChainMover? _mover;

        public AdjustNamespaceWindow()
        {
            InitializeComponent();
        }

        public AdjustNamespaceWindow(AdjustChainMover mover)
        {
            if (mover is null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            _mover = mover;

            InitializeComponent();
        }

        private void DialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetNextPage();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            SetNextPage();
        }

        private void SetNextPage()
        {
            if (!_mover!.TryToMove(Dispatcher, out var vm, out var uc))
            {
                Close();
                return;
            }

            ButtonGrid.IsEnabled = false;

            uc!.DataContext = vm;
            CenterContentControl.Content = uc;

            ThreadHelper.JoinableTaskFactory.RunAsync(
                async () => 
                {
                    try
                    {
                        await vm!.StartAsync();

                        ButtonGrid.IsEnabled = true;
                    }
                    catch (Exception excp)
                    {
                        CriticalErrorTextBlock.Text = excp.Message;
                        Logging.LogVS(excp);
                    }
                }).FileAndForget(nameof(SetNextPage));

    }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
