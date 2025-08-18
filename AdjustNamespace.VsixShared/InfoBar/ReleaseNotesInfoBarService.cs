﻿using AdjustNamespace.Command;
using AdjustNamespace.Options;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;

namespace AdjustNamespace.InfoBar
{
    public class ReleaseNotesInfoBarService : InfoBarService
    {
        private static readonly object _locker = new object();
        private static volatile ReleaseNotesInfoBarService? _instance;

        public static ReleaseNotesInfoBarService Instance => _instance!;

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

        public override void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var choose = (int)actionItem.ActionContext;

            General.Instance.LastVersion = Vsix.Version;
            General.Instance.Save();

            switch (choose)
            {
                case 1:
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
