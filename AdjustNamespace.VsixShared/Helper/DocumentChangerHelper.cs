using Microsoft.Build.Framework.XamlTypes;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Helper
{
    /// <summary>
    /// Helpers which apply a modification to a document of the Roslyn workspace.
    ///
    /// <c>Workspace.TryApplyChanges</c> fails if the solution has been changed by someone else
    /// after our snapshot has been taken, so the modification is built and applied in a loop
    /// until it succeeds.
    /// </summary>
    public static class DocumentChangerHelper
    {
        /// <summary>
        /// Apply a modification to the document with the given file path.
        /// </summary>
        /// <param name="workspace">Roslyn workspace.</param>
        /// <param name="filePath">Full path to the document to modify.</param>
        /// <param name="provider">
        /// Builder of the modified document; it takes the fresh document and its syntax root
        /// and returns the modified document (or <c>null</c> to cancel the modification).
        /// </param>
        public static async Task ApplyModifiedDocumentAsync(
            this Workspace workspace,
            string filePath,
            Func<Document, SyntaxNode, Document?> provider
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            bool r;
            do
            {
                var (document, syntaxRoot) = await workspace.GetDocumentAndSyntaxRootAsync(filePath);
                if (document == null || syntaxRoot == null)
                {
                    //skip this document
                    return;
                }

                var changedDocument = provider(document, syntaxRoot);
                if (changedDocument is null)
                {
                    return;
                }

                r = workspace.TryApplyChanges(changedDocument.Project.Solution);
            }
            while (!r);
        }

        /// <summary>
        /// Apply a modification to a document of the workspace.
        /// </summary>
        /// <param name="workspace">Roslyn workspace.</param>
        /// <param name="provider">
        /// Builder of the modified document (or <c>null</c> to cancel the modification).
        /// </param>
        public static async Task ApplyModifiedDocumentAsync(
            this Workspace workspace,
            Func<Workspace, Task<Document?>> provider
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (provider is null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            bool r;
            do
            {
                var changedDocument = await provider(workspace);
                if (changedDocument is null)
                {
                    return;
                }

                r = workspace.TryApplyChanges(changedDocument.Project.Solution);
            }
            while (!r);
        }
    }
}
