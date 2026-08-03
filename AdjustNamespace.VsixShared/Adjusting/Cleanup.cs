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
    /// <summary>
    /// The final stage of the adjusting: removal of the using clauses which point
    /// to the namespaces emptied by the adjusting.
    /// </summary>
    public class Cleanup
    {
        private readonly VsServices _vss;
        private readonly NamespaceCenter _namespaceCenter;

        /// <param name="vss">Visual Studio services.</param>
        /// <param name="namespaceCenter">Namespace state container which knows which namespaces became empty.</param>
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

        /// <summary>
        /// Remove the using clauses of the emptied namespaces from the given document.
        /// </summary>
        /// <param name="documentFilePath">Full path to the document to clean up.</param>
        public async Task RemoveEmptyUsingStatementsForAsync(
            string documentFilePath
            )
        {
            var workspace = _vss.Workspace;

            //see the comment in AddUsingFixer.FixAsync about this do-while
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

                //a file which several projects compile has a single text for all of them,
                //so a clause which is dead for one project may be alive for another one:
                //there is nothing we could do about it and we do not touch such a file
                var compilation = workspace.IsCompiledBySeveralProjects(documentFilePath)
                    ? null
                    : await document.Project.GetCompilationAsync()
                    ;

                var toRemove = _namespaceCenter.GetRemovedNamespaces(namespaces, compilation);
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
