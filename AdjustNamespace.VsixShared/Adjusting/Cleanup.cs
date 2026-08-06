using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AdjustNamespace.Namespace;
using AdjustNamespace.Roslyn;
using System.Threading;

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// The final stage of the adjusting: removal of the using clauses which point
    /// to the namespaces emptied by the adjusting.
    /// </summary>
    public class Cleanup
    {
        private readonly Workspace _workspace;
        private readonly NamespaceCenter _namespaceCenter;

        /// <param name="workspace">Roslyn workspace to clean up.</param>
        /// <param name="namespaceCenter">Namespace state container which knows which namespaces became empty.</param>
        public Cleanup(
            Workspace workspace,
            NamespaceCenter namespaceCenter
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            _workspace = workspace;
            _namespaceCenter = namespaceCenter;
        }

        /// <summary>
        /// Remove the using clauses of the emptied namespaces from the given document.
        /// </summary>
        /// <param name="documentFilePath">Full path to the document to clean up.</param>
        /// <param name="cancellationToken">
        /// Cancellation of the session. It is asked while the document is being read only:
        /// the removal itself is never interrupted in the middle.
        /// </param>
        public async Task RemoveEmptyUsingStatementsForAsync(
            string documentFilePath,
            CancellationToken cancellationToken = default
            )
        {
            var workspace = _workspace;

            //see the comment in DocumentChanger about this do-while
            bool r = true;
            do
            {
                var (document, syntaxRoot) = await workspace.GetDocumentAndSyntaxRootAsync(documentFilePath, cancellationToken);
                if (document == null || syntaxRoot == null)
                {
                    //something went wrong
                    //skip this document
                    return;
                }

                var namespaces = syntaxRoot.GetAllDescendants<UsingDirectiveSyntax>();

                //a file which several projects compile has a single text for all of them,
                //so a clause is dead for that file only if it is dead for every one of them
                var compilations = new List<Compilation>();
                foreach (var fileDocument in workspace.GetDocuments(documentFilePath))
                {
                    var compilation = await fileDocument.Project.GetCompilationAsync(cancellationToken);
                    if (compilation != null)
                    {
                        compilations.Add(compilation);
                    }
                }

                var toRemove = _namespaceCenter.GetRemovedNamespaces(namespaces, compilations);
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
