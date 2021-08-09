using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Windows;
using Task = System.Threading.Tasks.Task;

namespace AdjustNamespace.Window
{
    /// <summary>
    /// Interaction logic for AdjustNamespaceWindow.xaml
    /// </summary>
    public partial class AdjustNamespaceWindow : DialogWindow
    {
        private readonly Func<AdjustNamespaceWindow, Task> _factory;

        public AdjustNamespaceWindow(
            Func<AdjustNamespaceWindow, Task> factory
            )
        {
            if (factory is null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _factory = factory;

            InitializeComponent();
        }


        private async void DialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await _factory(this);
        }
    }
}
