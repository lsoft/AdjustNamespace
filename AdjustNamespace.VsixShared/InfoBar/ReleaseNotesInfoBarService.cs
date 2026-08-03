using AdjustNamespace.Command;
using AdjustNamespace.Options;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;

namespace AdjustNamespace.InfoBar
{
    /// <summary>
    /// The gold bar which is shown once after the extension has been updated
    /// and offers the user to read the release notes.
    /// </summary>
    public class ReleaseNotesInfoBarService : InfoBarService
    {
        private static readonly object _locker = new object();
        private static volatile ReleaseNotesInfoBarService? _instance;

        /// <summary>
        /// The singleton instance. <see cref="Initialize"/> must be called before.
        /// </summary>
        public static ReleaseNotesInfoBarService Instance => _instance!;

        /// <summary>
        /// Create the singleton instance (does nothing if it exists already).
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider)
        {
            if (_instance is null)
            {
                lock (_locker)
                {
                    if (_instance is null)
                    {
                        _instance = new ReleaseNotesInfoBarService(
                            serviceProvider
                            );
                    }
                }
            }
        }

        public ReleaseNotesInfoBarService(
            IServiceProvider serviceProvider
            )
            : base(serviceProvider)
        {
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Whatever the user has chosen, the current version is remembered,
        /// so the bar is not shown again until the next update.
        /// </remarks>
        public override void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var choose = (int)actionItem.ActionContext;

            General.Instance.LastVersion = Vsix.Version;
            General.Instance.Save();

            switch (choose)
            {
                case 1:
                    //`Show release notes` has been clicked
                    var shell = (IVsUIShell)_serviceProvider.GetService(typeof(SVsUIShell));
                    shell.PostExecCommand(
                        ShowReleaseNotesCommand.CommandSet,
                        ShowReleaseNotesCommand.CommandId,
                        0,
                        null
                        );
                    break;
                default:
                    break;
            }

            infoBarUIElement.Close();
        }


        /// <inheritdoc/>
        protected override InfoBarModel GetModel()
        {
            return new InfoBarModel(
                new InfoBarTextSpan[]
                {
                    new InfoBarTextSpan("New version of Adjust namespaces has been installed")
                },
                new InfoBarActionItem[]
                {
                        new InfoBarHyperlink("Show release notes", 1),
                        new InfoBarHyperlink("Not interested", 2)
                },
                KnownMonikers.Namespace,
                isCloseButtonVisible: false
                );
        }

    }
}
