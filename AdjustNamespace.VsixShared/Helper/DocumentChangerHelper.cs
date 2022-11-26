using Microsoft.Build.Framework.XamlTypes;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace.Helper
{
    public static class DocumentChangerHelper
    {
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
