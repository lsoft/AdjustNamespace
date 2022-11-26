using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AdjustNamespace.Helper;
using System.Threading;

namespace AdjustNamespace.Adjusting
{
    public class Cleanup
    {
        private readonly VsServices _vss;
        private readonly NamespaceCenter _namespaceCenter;

        public Cleanup(
            VsServices vss,
            NamespaceCenter namespaceCenter
            )
        {
            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            _vss = vss;
            _namespaceCenter = namespaceCenter;
        }

        public async Task RemoveEmptyUsingStatementsAsync(
            CancellationToken cancellationToken
            )
        {
            var workspace = _vss.Workspace;

            foreach (var documentFilePath in workspace.EnumerateAllDocumentFilePaths(Predicate.IsProjectInScope, Predicate.IsDocumentInScope))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                bool r = true;
                do
                {
                    var (document, syntaxRoot) = await workspace.GetDocumentAndSyntaxRootAsync(documentFilePath);
                    if (document == null || syntaxRoot == null)
                    {
                        //something went wrong
                        //skip this document
                        return;
                    }

                    var namespaces = syntaxRoot.GetAllDescendants<UsingDirectiveSyntax>();

                    var toRemove = _namespaceCenter.GetRemovedNamespaces(namespaces);
                    if (toRemove.Count == 0)
                    {
                        continue;
                    }

                    syntaxRoot = syntaxRoot.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepNoTrivia);
                    if (syntaxRoot != null)
                    {
                        var changedDocument = document.WithSyntaxRoot(syntaxRoot);

                        r = workspace.TryApplyChanges(changedDocument.Project.Solution);
                    }
                }
                while (!r);
            }
        }

    }
}
