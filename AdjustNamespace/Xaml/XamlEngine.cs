using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdjustNamespace.Xaml
{
    public class XamlEngine
    {
        private readonly XamlDocument _document;

        public XamlEngine(string xamlFilePath)
        {
            if (xamlFilePath is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePath));
            }

            _document = new XamlDocument(
                xamlFilePath
                );
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

        internal bool GetRootInfo(out string? rootNamespace, out string? rootName)
        {
            return _document.GetRootInfo(out rootNamespace, out rootName);
        }
    }
}
