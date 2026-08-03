using Microsoft.VisualStudio.Shell.Interop;

namespace AdjustNamespace.InfoBar
{
    /// <summary>
    /// Base class of the gold bars (info bars) shown in the main window of Visual Studio.
    /// </summary>
    public abstract class InfoBarService : IVsInfoBarUIEvents
    {

        protected readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Subscription cookie of the shown bar.
        /// </summary>
        private uint _cookie;

        protected InfoBarService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// The bar has been closed: unsubscribe from its events.
        /// </summary>
        public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
        {
            infoBarUIElement.Unadvise(_cookie);
        }

        /// <summary>
        /// The user has clicked a hyperlink of the bar.
        /// </summary>
        public abstract void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem);

        /// <summary>
        /// Show the bar in the main window of Visual Studio. Requires the main thread.
        /// </summary>
        public void ShowInfoBar()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var shell = (IVsShell)_serviceProvider.GetService(typeof(SVsShell));
            if (shell != null)
            {
                shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out var obj);
                var host = (IVsInfoBarHost)obj;

                if (host == null)
                {
                    return;
                }

                var infoBarModel = GetModel();

                var factory = (IVsInfoBarUIFactory)_serviceProvider.GetService(typeof(SVsInfoBarUIFactory));
                var element = factory.CreateInfoBar(infoBarModel);
                element.Advise(this, out _cookie);
                host.AddInfoBar(element);
            }
        }

        /// <summary>
        /// Content of the bar: its text, its hyperlinks and its icon.
        /// </summary>
        protected abstract InfoBarModel GetModel();
    }
}
