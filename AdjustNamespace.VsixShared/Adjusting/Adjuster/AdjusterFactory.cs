using AdjustNamespace.Helper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Adjuster
{
    public class AdjusterFactory
    {
        private readonly VsServices _vss;
        private readonly NamespaceReplaceRegex _replaceRegex;
        private readonly bool _openFilesToEnableUndo;
        private readonly NamespaceCenter _namespaceCenter;
        private readonly List<string> _xamlFilePaths;

        public static async Task<AdjusterFactory> CreateAsync(
            VsServices vss,
            NamespaceReplaceRegex replaceRegex,
            bool openFilesToEnableUndo,
            NamespaceCenter namespaceCenter
            )
        {
            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            //get all xaml files in current solution
            var filePaths = await SolutionHelper.GetAllFilesFromAsync();
            var xamlFilePaths = filePaths.FindAll(fp => fp.EndsWith(".xaml"));

            return new AdjusterFactory(
                vss,
                replaceRegex,
                openFilesToEnableUndo,
                namespaceCenter,
                xamlFilePaths
                );

        }

        private AdjusterFactory(
            VsServices vss,
            NamespaceReplaceRegex replaceRegex,
            bool openFilesToEnableUndo,
            NamespaceCenter namespaceCenter,
            List<string> xamlFilePaths
            )
        {
            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            if (xamlFilePaths is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePaths));
            }

            _vss = vss;
            _replaceRegex = replaceRegex;
            _openFilesToEnableUndo = openFilesToEnableUndo;
            _namespaceCenter = namespaceCenter;
            _xamlFilePaths = xamlFilePaths;
        }

        public async Task<IAdjuster?> CreateAsync(
            string subjectFilePath
            )
        {
            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            var pii = await SolutionHelper.TryGetProjectItemAsync(subjectFilePath);
            if (!pii.HasValue)
            {
                return null;
            }

            var targetNamespace = await NamespaceHelper.TryDetermineTargetNamespaceAsync(
                pii.Value.Project,
                _vss,
                _replaceRegex,
                subjectFilePath
                );
            if (string.IsNullOrEmpty(targetNamespace))
            {
                return null;
            }

            if (subjectFilePath.EndsWith(".xaml"))
            {
                //it's a xaml

                var xamlAdjuster = new XamlAdjuster(
                    _vss,
                    _openFilesToEnableUndo,
                    subjectFilePath,
                    targetNamespace!
                    );
                return xamlAdjuster;
            }
            else
            {
                //we can do nothing with not a C# documents
                var subjectDocument = _vss.Workspace.GetDocument(subjectFilePath);
                if (!subjectDocument.IsDocumentInScope())
                {
                    return null;
                }

                var csAdjuster = new CsAdjuster(
                    _vss,
                    _openFilesToEnableUndo,
                    _namespaceCenter,
                    subjectFilePath,
                    targetNamespace!,
                    _xamlFilePaths
                    );

                return csAdjuster;
            }
        }
    }
}
