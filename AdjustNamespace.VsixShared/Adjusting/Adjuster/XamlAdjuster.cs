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
        private readonly VsServices _vss;
        private readonly string _subjectFilePath;
        private readonly string _targetNamespace;
        private readonly bool _openFilesToEnableUndo;

        public XamlAdjuster(
            VsServices vss,
            bool openFilesToEnableUndo,
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

            _vss = vss;
            _openFilesToEnableUndo = openFilesToEnableUndo;
            _subjectFilePath = subjectFilePath;
            _targetNamespace = targetNamespace;
        }

        public async Task<bool> AdjustAsync()
        {
            var xamlEngine = await XamlEngine.CreateAsync(
                _vss,
                _openFilesToEnableUndo,
                _subjectFilePath
                );

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

            xamlEngine.SaveIfChangesExists();

            return true;
        }
    }
}
