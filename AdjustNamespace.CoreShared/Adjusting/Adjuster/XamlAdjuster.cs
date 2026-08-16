using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Xaml;
using AdjustNamespace.Xaml.BodyProvider;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Adjuster
{
    /// <summary>
    /// Adjuster for xaml file.
    /// It changes the `x:Class` attribute of the root element only
    /// (the code behind file is adjusted separately by <see cref="CsAdjuster"/>).
    ///
    /// Whether this file is a subject to change at all has been decided by
    /// <see cref="AdjustPlanner"/> already.
    /// </summary>
    public class XamlAdjuster : IAdjuster
    {
        private readonly IXamlBodyProviderFactory _xamlBodyProviderFactory;
        private readonly string _subjectFilePath;
        private readonly string _targetNamespace;
        private readonly bool _openFilesToEnableUndo;

        /// <param name="xamlBodyProviderFactory">How the xaml file is read and written.</param>
        /// <param name="openFilesToEnableUndo">Open the changed file in the editor (this allows the user to undo the changes).</param>
        /// <param name="plan">What has to happen with the file.</param>
        public XamlAdjuster(
            IXamlBodyProviderFactory xamlBodyProviderFactory,
            bool openFilesToEnableUndo,
            AdjustPlanItem plan
            )
        {
            if (xamlBodyProviderFactory is null)
            {
                throw new ArgumentNullException(nameof(xamlBodyProviderFactory));
            }

            _xamlBodyProviderFactory = xamlBodyProviderFactory;
            _openFilesToEnableUndo = openFilesToEnableUndo;
            _subjectFilePath = plan.FilePath;
            _targetNamespace = plan.TargetNamespace;
        }

        /// <inheritdoc/>
        public async Task<bool> AdjustAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (xamlDocument, modifiedDocument) = await TryModifyDocumentAsync();
            if (!modifiedDocument.HasValue)
            {
                return false;
            }

            modifiedDocument.Value.SaveIfChangesExistsAgainst(xamlDocument);

            return true;
        }

        /// <summary>
        /// Check (without saving anything) if this xaml file is a subject to change.
        /// Used by <see cref="SubjectFileCollector"/> to show the user only those files
        /// which will really be modified.
        /// </summary>
        public async Task<bool> IsChangesExistsAsync(
            )
        {
            var (xamlDocument, modifiedDocument) = await TryModifyDocumentAsync();
            return modifiedDocument.HasValue && modifiedDocument.Value.IsChangesExists(xamlDocument);
        }

        /// <summary>
        /// Build the modified version of the subject xaml document.
        /// </summary>
        /// <returns>
        /// The original document and its modified copy. The modified copy is <c>null</c>
        /// if the document has no root class at all or it is in the target namespace already.
        /// </returns>
        private async Task<(XamlDocument, XamlDocument?)> TryModifyDocumentAsync(
            )
        {
            var xamlEngine = new XamlEngine(_xamlBodyProviderFactory);

            var xamlDocument = await xamlEngine.CreateDocumentAsync(
                _openFilesToEnableUndo,
                _subjectFilePath
                );

            if (!xamlDocument.GetRootInfo(out var rootNamespace, out var rootName))
            {
                return (xamlDocument, null);
            }

            if (rootNamespace == _targetNamespace)
            {
                return (xamlDocument, null);
            }

            var modifiedXamlDocument = xamlDocument.MoveObject(
                rootNamespace!,
                rootName!,
                _targetNamespace
                );

            return (xamlDocument, modifiedXamlDocument);
        }
    }
}
