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

        /// <param name="xamlBodyProviderFactory">How the xaml file is read and written.</param>
        /// <param name="plan">What has to happen with the file.</param>
        public XamlAdjuster(
            IXamlBodyProviderFactory xamlBodyProviderFactory,
            AdjustPlanItem plan
            )
        {
            if (xamlBodyProviderFactory is null)
            {
                throw new ArgumentNullException(nameof(xamlBodyProviderFactory));
            }

            _xamlBodyProviderFactory = xamlBodyProviderFactory;
            _subjectFilePath = plan.FilePath;
            _targetNamespace = plan.TargetNamespace;
        }

        /// <inheritdoc/>
        public async Task<bool> AdjustAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var xamlEngine = new XamlEngine(_xamlBodyProviderFactory);

            using var xamlDocument = await xamlEngine.CreateForWriteAsync(_subjectFilePath);

            if (!TryBuildModified(xamlDocument, out var modifiedDocument))
            {
                return false;
            }

            modifiedDocument.SaveIfChangesExistsAgainst(xamlDocument);

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
            var xamlEngine = new XamlEngine(_xamlBodyProviderFactory);

            using var xamlDocument = await xamlEngine.CreateForReadAsync(_subjectFilePath);

            if (!TryBuildModified(xamlDocument, out var modifiedDocument))
            {
                return false;
            }

            return modifiedDocument.IsChangesExists(xamlDocument);
        }

        /// <summary>
        /// Build the modified version of the subject xaml document.
        /// </summary>
        /// <returns>
        /// <c>true</c> when the root class exists and is not already in the target namespace.
        /// </returns>
        private bool TryBuildModified(
            XamlDocument xamlDocument,
            out XamlDocument modifiedDocument
            )
        {
            modifiedDocument = default;

            if (!xamlDocument.GetRootInfo(out var rootNamespace, out var rootName))
            {
                return false;
            }

            if (rootNamespace == _targetNamespace)
            {
                return false;
            }

            modifiedDocument = xamlDocument.MoveObject(
                rootNamespace!,
                rootName!,
                _targetNamespace
                );

            return true;
        }
    }
}
