using AdjustNamespace.Helper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Adjuster
{
    /// <summary>
    /// Factory which creates a suitable <see cref="IAdjuster"/> for the given file:
    /// <see cref="XamlAdjuster"/> for a xaml file and <see cref="CsAdjuster"/> for a C# file.
    /// It also determines the target namespace for that file and filters out the files
    /// we are unable to process (a file outside of its project folder, a file of a project
    /// without Roslyn support etc.).
    /// </summary>
    public class AdjusterFactory
    {
        private readonly VsServices _vss;
        private readonly NamespaceReplaceRegex _replaceRegex;
        private readonly bool _openFilesToEnableUndo;
        private readonly NamespaceCenter _namespaceCenter;

        /// <summary>
        /// All the xaml files of the solution. A C# type moved to another namespace
        /// may be referenced from any of them, so all of them are the subject to check.
        /// </summary>
        private readonly List<string> _xamlFilePaths;

        /// <summary>
        /// Create a factory. Performs an expensive scan of the solution for xaml files,
        /// so it is intended to be created once per adjusting session.
        /// </summary>
        /// <param name="vss">Visual Studio services.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        /// <param name="openFilesToEnableUndo">Open the changed files in the editor (this allows the user to undo the changes).</param>
        /// <param name="namespaceCenter">Namespace state container shared across the whole adjusting session.</param>
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

        /// <summary>
        /// Create an adjuster for the given file.
        /// </summary>
        /// <param name="subjectFilePath">Full path to the file to adjust.</param>
        /// <returns>
        /// An adjuster, or <c>null</c> if the target namespace cannot be determined
        /// or the file cannot be processed at all.
        /// </returns>
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
