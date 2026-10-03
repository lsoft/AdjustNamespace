using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdjustNamespace.Namespace;
using AdjustNamespace.Roslyn;
using System.Threading;
using AdjustNamespace;

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// The final stage of the adjusting: removal of the using clauses which point
    /// to the namespaces emptied by the adjusting, and of the <c>nameof</c> of them.
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

                var originalRoot = syntaxRoot;

                syntaxRoot = await ReplaceNameofsOfRemovedNamespacesAsync(
                    document,
                    syntaxRoot,
                    compilations,
                    cancellationToken
                    );

                var namespaces = syntaxRoot.GetAllDescendants<UsingDirectiveSyntax>();

                var toRemove = _namespaceCenter.GetRemovedNamespaces(namespaces, compilations);
                if (toRemove.Count > 0)
                {
                    AdjustLog.WriteLine(
                        $"[Adjust] Cleanup: {documentFilePath}: removing using(s) of "
                        + string.Join(", ", toRemove.OfType<UsingDirectiveSyntax>().Select(u => u.Name?.ToString() ?? "<unnamed>"))
                        );

                    syntaxRoot = syntaxRoot.RemoveNodes(toRemove, SyntaxRemoveOptions.KeepNoTrivia);
                }

                if (syntaxRoot != null && !ReferenceEquals(syntaxRoot, originalRoot))
                {
                    var changedDocument = document.WithSyntaxRoot(syntaxRoot);

                    r = workspace.TryApplyChanges(changedDocument.Project.Solution);
                }
            }
            while (!r);
        }

        /// <summary>
        /// Replace <c>nameof(A.B)</c> of an emptied namespace with the string it gives,
        /// <c>"B"</c>: the namespace does not exist anymore and such a <c>nameof</c> does not
        /// compile. The string is kept as it is rather than following the move, because the
        /// program sees it, and the types of the namespace may have moved into several ones.
        /// </summary>
        private async Task<SyntaxNode> ReplaceNameofsOfRemovedNamespacesAsync(
            Document document,
            SyntaxNode syntaxRoot,
            IReadOnlyList<Compilation> compilations,
            CancellationToken cancellationToken
            )
        {
            var nameofs = syntaxRoot
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
                .Where(i => i.ArgumentList.Arguments.Count == 1)
                .Where(i => i.ArgumentList.Arguments[0].Expression is NameSyntax
                    || i.ArgumentList.Arguments[0].Expression is MemberAccessExpressionSyntax)
                .ToList();

            if (nameofs.Count == 0)
            {
                return syntaxRoot;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel == null)
            {
                return syntaxRoot;
            }

            var toReplace = new Dictionary<SyntaxNode, string>();

            foreach (var nameof in nameofs)
            {
                var argument = nameof.ArgumentList.Arguments[0].Expression;

                var symbolInfo = semanticModel.GetSymbolInfo(argument, cancellationToken);
                if (symbolInfo.Symbol != null || symbolInfo.CandidateSymbols.Length > 0)
                {
                    //the name means something still
                    continue;
                }

                var writtenName = NamespaceHelper.NormalizeUsingName(argument.ToString());
                if (writtenName.Length == 0)
                {
                    continue;
                }

                if (!CandidatesOf(argument, writtenName).Any(c => _namespaceCenter.IsRemoved(c, compilations)))
                {
                    continue;
                }

                var value = writtenName.Substring(writtenName.LastIndexOf('.') + 1);

                AdjustLog.WriteLine($"[Adjust] Cleanup: {document.FilePath}: '{nameof}' names an emptied namespace -> \"{value}\"");

                toReplace[nameof] = value;
            }

            if (toReplace.Count == 0)
            {
                return syntaxRoot;
            }

            return syntaxRoot.ReplaceNodes(
                toReplace.Keys,
                (original, _) => SyntaxFactory
                    .LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(toReplace[original]))
                    .WithTriviaFrom(original)
                );
        }

        /// <summary>
        /// The full names a namespace name written at the given place may mean: relative to
        /// every enclosing namespace, from the innermost one, and as a root one.
        /// </summary>
        private static IEnumerable<string> CandidatesOf(SyntaxNode place, string writtenName)
        {
            if (place.ToString().TrimStart().StartsWith("global::", StringComparison.Ordinal))
            {
                yield return writtenName;
                yield break;
            }

            var enclosing = string.Join(
                ".",
                place.Ancestors()
                    .OfType<BaseNamespaceDeclarationSyntax>()
                    .Reverse()
                    .Select(n => NamespaceHelper.NormalizeUsingName(n.Name.ToString()))
                );

            while (enclosing.Length > 0)
            {
                yield return enclosing + "." + writtenName;

                var dotIndex = enclosing.LastIndexOf('.');
                enclosing = dotIndex >= 0 ? enclosing.Substring(0, dotIndex) : string.Empty;
            }

            yield return writtenName;
        }

    }
}
