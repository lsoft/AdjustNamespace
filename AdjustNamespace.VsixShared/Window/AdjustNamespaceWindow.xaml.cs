using AdjustNamespace.Options;
using AdjustNamespace.UI.StepFactory;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;

namespace AdjustNamespace.Window
{
    /// <summary>
    /// Interaction logic for AdjustNamespaceWindow.xaml
    /// </summary>
    public partial class AdjustNamespaceWindow : DialogWindow
    {
        private readonly Func<AdjustNamespaceWindow, System.Threading.Tasks.Task> _factory;

        public AdjustNamespaceWindow(
            Func<AdjustNamespaceWindow, System.Threading.Tasks.Task> factory
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
            try
            {
                var showAwardCheckBox = false;
                if (!General.Instance.StarsGiven)
                {
                    if (General.Instance.FilesAdjusted >= 20)
                    {
                        showAwardCheckBox = true;
                    }
                }

                this.AwardCheckBox.Visibility = showAwardCheckBox ? Visibility.Visible : Visibility.Collapsed;

                await _factory(this);
            }
            catch (Exception ex)
            {
                Logging.LogVS(ex);
            }
        }

        private void DialogWindow_Closed(object sender, EventArgs e)
        {
            if(this.AwardCheckBox.IsChecked.GetValueOrDefault(false))
            {
                General.Instance.StarsGiven = true;

                System.Diagnostics.Process.Start("https://marketplace.visualstudio.com/items?itemName=lsoft.AdjustNamespaceVisualStudioExtension2022&ssr=false#review-details");
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
