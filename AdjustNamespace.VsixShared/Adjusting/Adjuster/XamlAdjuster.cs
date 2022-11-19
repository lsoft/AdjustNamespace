using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Xaml;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// Adjuster for xaml file.
    /// </summary>
    public class XamlAdjuster : IAdjuster
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

        public Task<bool> AdjustAsync()
        {
            var xamlEngine = new XamlEngine(_subjectFilePath);

            if (!xamlEngine.GetRootInfo(out var rootNamespace, out var rootName))
            {
                return Task.FromResult(false);
            }

            if (rootNamespace == _targetNamespace)
            {
                return Task.FromResult(false);
            }

            xamlEngine.MoveObject(
                rootNamespace!,
                rootName!,
                _targetNamespace
                );

            xamlEngine.SaveIfChangesExists();

            return Task.FromResult(true);
        }
    }
}
