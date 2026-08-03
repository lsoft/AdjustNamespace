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
    ///
    /// The modal wizard window. It hosts the steps one by one in its content control,
    /// see <see cref="Create"/> and the <c>AdjustNamespace.UI.StepFactory</c> namespace.
    /// </summary>
    public partial class AdjustNamespaceWindow : DialogWindow
    {
        /// <summary>
        /// Builder of the first wizard step. It is invoked when the window is loaded.
        /// </summary>
        private readonly Func<AdjustNamespaceWindow, System.Threading.Tasks.Task> _factory;

        /// <param name="factory">Builder of the first wizard step.</param>
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


        /// <summary>
        /// Show the first wizard step and (from time to time) the `please rate this extension` checkbox.
        /// </summary>
        private async void DialogWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //ask for a rating only if the user has already adjusted something
                //and has not rated the extension yet
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

        /// <summary>
        /// Open the marketplace page if the user has agreed to rate the extension.
        /// </summary>
        private void DialogWindow_Closed(object sender, EventArgs e)
        {
            if(this.AwardCheckBox.IsChecked.GetValueOrDefault(false))
            {
                General.Instance.StarsGiven = true;

                System.Diagnostics.Process.Start("https://marketplace.visualstudio.com/items?itemName=lsoft.AdjustNamespaceVisualStudioExtension2022&ssr=false#review-details");
            }
        }

        /// <summary>
        /// Build the wizard window with its chain of steps:
        /// preparation -> selection -> performing.
        /// </summary>
        /// <param name="vss">Visual Studio services.</param>
        /// <param name="filePaths">Full paths of the files chosen by the user.</param>
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

                    selsf.PreviousStepFactory = prepsf;

                    await prepsf.CreateAsync(filePaths);
                }
                );

            return window;
        }
    }
}
