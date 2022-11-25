using AdjustNamespace.Options;
using AdjustNamespace.UI.StepFactory;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Windows;
using static AdjustNamespace.Options.DialogPageProvider;
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
            var showAwardCheckBox = false;
            if (!GeneralOptions.Instance.StarsGiven)
            {
                if (GeneralOptions.Instance.FilesAdjusted >= 20)
                {
                    showAwardCheckBox = true;
                }
            }

            this.AwardCheckBox.Visibility = showAwardCheckBox ? Visibility.Visible : Visibility.Collapsed;

            await _factory(this);
        }

        private void DialogWindow_Closed(object sender, EventArgs e)
        {
            if(this.AwardCheckBox.IsChecked.GetValueOrDefault(false))
            {
                GeneralOptions.Instance.StarsGiven = true;

#if VS2022
                System.Diagnostics.Process.Start("https://marketplace.visualstudio.com/items?itemName=lsoft.AdjustNamespaceVisualStudioExtension2022&ssr=false#review-details");
#else
                System.Diagnostics.Process.Start("https://marketplace.visualstudio.com/items?itemName=lsoft.AdjustNamespaceVisualStudioExtension&ssr=false#review-details");
#endif
            }
        }
        public static AdjustNamespaceWindow Create(
            VsServices vss,
            HashSet<string> filePaths
            )
        {
            var window = new AdjustNamespaceWindow(
                async anw =>
                {
                    var perfsf = new PerformingStepFactory(
                        vss,
                        anw,
                        anw.CenterContentControl
                        );

                    var selsf = new SelectedStepFactory(
                        vss,
                        anw.CenterContentControl,
                        perfsf
                        );

                    var prepsf = new PreparationStepFactory(
                        vss,
                        anw.CenterContentControl,
                        selsf
                        );

                    await prepsf.CreateAsync(filePaths);
                }
                );

            return window;
        }
    }
}
