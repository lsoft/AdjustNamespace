using AdjustNamespace.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting
{
    public class XamlAdjuster
    {
        private readonly string _subjectFilePath;
        private readonly string _targetNamespace;

        public XamlAdjuster(
            string subjectFilePath,
            string targetNamespace
            )
        {
            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            if (targetNamespace is null)
            {
                throw new ArgumentNullException(nameof(targetNamespace));
            }

            _subjectFilePath = subjectFilePath;
            _targetNamespace = targetNamespace;
        }

        public bool Adjust()
        {
            var xamlEngine = new XamlEngine(_subjectFilePath);

            if (!xamlEngine.GetRootInfo(out var rootNamespace, out var rootName))
            {
                return false;
            }

            if (rootNamespace == _targetNamespace)
            {
                return false;
            }

            xamlEngine.MoveObject(
                rootNamespace!,
                rootName!,
                _targetNamespace
                );
            xamlEngine.RemoveObsoleteClrNamespaces();
            xamlEngine.SaveIfChangesExists();

            return true;
        }
    }
}
