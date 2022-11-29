using AdjustNamespace.Helper;
using AdjustNamespace.Xaml.BodyProvider;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using System;

namespace AdjustNamespace.Xaml
{
    public class XamlEngine
    {
        private readonly XamlDocument _document;

        public bool ChangesExists => _document.ChangesExists;

        private XamlEngine(
            IXamlBodyProvider bodyProvider
            )
        {
            if (bodyProvider is null)
            {
                throw new ArgumentNullException(nameof(bodyProvider));
            }

            _document = new XamlDocument(
                bodyProvider
                );
        }

        public static async System.Threading.Tasks.Task<XamlEngine> CreateAsync(
            VsServices vss,
            bool openFilesToEnableUndo,
            string xamlFilePath
            )
        {
            IXamlBodyProvider bodyProvider;
            if (openFilesToEnableUndo)
            {
                var obp = new OpenedXamlBodyProvider(vss, xamlFilePath);
                await obp.OpenAsync();

                bodyProvider = obp;
            }
            else
            {
                bodyProvider = new ClosedXamlBodyProvider(xamlFilePath);
            }

            return new XamlEngine(bodyProvider);
        }

        public void MoveObject(
            string sourceNamespace,
            string objectClassName,
            string targetNamespace
            )
        {
            if (sourceNamespace is null)
            {
                throw new ArgumentNullException(nameof(sourceNamespace));
            }

            if (objectClassName is null)
            {
                throw new ArgumentNullException(nameof(objectClassName));
            }

            if (targetNamespace is null)
            {
                throw new ArgumentNullException(nameof(targetNamespace));
            }

            _document.MoveObject(
                sourceNamespace,
                objectClassName,
                targetNamespace
                );

            _document.Reload();

            _document.Cleanup();

            _document.Reload();
        }

        public void SaveIfChangesExists(
            )
        {
            _document.SaveIfChangesExists();
        }

        public bool GetRootInfo(out string? rootNamespace, out string? rootName)
        {
            return _document.GetRootInfo(out rootNamespace, out rootName);
        }
    }
}
