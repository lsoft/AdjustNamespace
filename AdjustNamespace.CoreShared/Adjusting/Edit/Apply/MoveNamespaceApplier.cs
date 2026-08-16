using AdjustNamespace;
using AdjustNamespace.Roslyn;
using AdjustNamespace.Namespace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Edit.Apply
{
    /// <summary>
    /// Applies a <see cref="MoveNamespaceEdit"/>.
    /// Both the classic (<c>namespace A { }</c>) and the file scoped (<c>namespace A;</c>)
    /// declarations are supported.
    /// </summary>
    public static class MoveNamespaceApplier
    {
        public static async Task ApplyAsync(
            Workspace workspace,
            string filePath,
            NamespaceTransition transition
            )
        {
            //the using clause is added first: it shifts the spans of everything which follows it
            await AddUsingOfTheOldNamespaceAsync(workspace, filePath, transition);
            await RenameNamespaceAsync(workspace, filePath, transition);
        }

        /// <summary>
        /// class A : IA {}
        /// we're moving A into a different namespace, but IA are not.
        /// we need to insert 'using old namespace'
        /// otherwise ia will not be resolved
        ///
        /// we can't determite it is the case or it's not without a costly analysis
        /// it's a subject for a future work
        /// so add at 100% cases now
        ///
        /// ...with a single exception: if the projects which compile this file have nothing
        /// in that namespace anymore, such a clause does not compile. The namespace may stay
        /// alive for the solution and be gone for these projects at the same time (another
        /// project fills it and these ones do not reference it), and then the cleanup has
        /// no reason to remove the clause again.
        /// </summary>
        private static async Task AddUsingOfTheOldNamespaceAsync(
            Workspace workspace,
            string filePath,
            NamespaceTransition transition
            )
        {
            if ((await FindNamespaceNameSpansAsync(workspace, filePath, transition.OriginalName)).Count == 0)
            {
                //this file does not declare that namespace, there is nothing to move
                AdjustLog.WriteLine(
                    $"[Adjust] MoveNamespace: {filePath}: no declaration of {transition.OriginalName}, using clause skipped"
                    );
                return;
            }

            if (!await workspace.IsNamespaceAliveOutsideAsync(filePath, transition.OriginalName))
            {
                AdjustLog.WriteLine(
                    $"[Adjust] MoveNamespace: {filePath}: {transition.OriginalName} is empty outside this file, using clause skipped"
                    );
                return;
            }

            await workspace.ApplyModifiedDocumentAsync(
                filePath,
                (document, syntaxRoot) =>
                {
                    var cus = syntaxRoot as CompilationUnitSyntax;
                    if (cus == null)
                    {
                        //skip this namespace
                        return null;
                    }

                    if (HasUsingOf(cus, transition.OriginalName))
                    {
                        AdjustLog.WriteLine(
                            $"[Adjust] MoveNamespace: {filePath}: using {transition.OriginalName} already present"
                            );
                        return null;
                    }

                    AdjustLog.WriteLine(
                        $"[Adjust] MoveNamespace: {filePath}: add using {transition.OriginalName} "
                        + $"(namespace stays alive while moving to {transition.ModifiedName})"
                        );

                    var newUsingStatement = SyntaxFactory.UsingDirective(
                        SyntaxFactory.ParseName(
                            " " + transition.OriginalName
                            )
                        ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                    return document.WithSyntaxRoot(
                        cus.AddUsingKeepingHeader(newUsingStatement)
                        );
                }
                );
        }

        /// <summary>
        /// Replace the name of every declaration of the old namespace with the new one.
        /// Only the name is replaced and not the whole declaration: everything else of the
        /// file (including the declarations which are a disabled text for the project we are
        /// looking at) stays exactly as the user has written it.
        /// </summary>
        private static async Task RenameNamespaceAsync(
            Workspace workspace,
            string filePath,
            NamespaceTransition transition
            )
        {
            await workspace.ApplyModifiedDocumentAsync(
                async ws =>
                {
                    //the spans are rebuilt on every attempt: a failed TryApplyChanges means
                    //the file has been changed by someone else in the meantime
                    var spans = await FindNamespaceNameSpansAsync(ws, filePath, transition.OriginalName);
                    if (spans.Count == 0)
                    {
                        //skip this namespace
                        return null;
                    }

                    var (document, syntaxRoot) = await ws.GetDocumentAndSyntaxRootAsync(filePath);
                    if (document == null || syntaxRoot == null)
                    {
                        //something went wrong
                        //skip this document
                        return null;
                    }

                    return document.WithText(
                        syntaxRoot.GetText().WithChanges(
                            spans.ConvertAll(s => new TextChange(s, transition.ModifiedName))
                            )
                        );
                }
                );
        }

        /// <summary>
        /// The file imports the given namespace already.
        /// <c>using Alias = A.B;</c> and <c>using static A.B;</c> do not import it,
        /// see <see cref="AddUsingApplier"/>.
        /// </summary>
        private static bool HasUsingOf(CompilationUnitSyntax cus, string namespaceName)
        {
            return cus
                .GetAllDescendants<UsingDirectiveSyntax>()
                .Any(s =>
                    s.Alias == null
                    && s.StaticKeyword.IsKind(SyntaxKind.None)
                    //Name is nullable since Roslyn 5 (`using unsafe int*;` has no name)
                    && NamespaceHelper.NormalizeUsingName(s.Name?.ToString() ?? string.Empty)
                        == namespaceName
                    );
        }

        /// <summary>
        /// The spans of the names of every declaration of the given namespace in the file.
        ///
        /// A file which several projects compile has a syntax tree per project, and
        /// a declaration which is guarded by a conditional compilation symbol is a code for
        /// a part of them and a disabled text for the rest, so all of these trees are asked.
        /// There is a single text behind all of them, hence a span of any one of them
        /// is a span of the file.
        /// </summary>
        private static async Task<List<TextSpan>> FindNamespaceNameSpansAsync(
            Workspace workspace,
            string filePath,
            string namespaceName
            )
        {
            var result = new List<TextSpan>();

            foreach (var syntaxRoot in await workspace.GetSyntaxRootsAsync(filePath))
            {
                if (!syntaxRoot.TryFindNamespaceNodesFor(namespaceName, out var foundNamespaces))
                {
                    continue;
                }

                foreach (var foundNamespace in foundNamespaces)
                {
                    var span = foundNamespace.Name.Span;

                    if (!result.Contains(span))
                    {
                        result.Add(span);
                    }
                }
            }

            //the changes of a text have to be ordered and must not intersect;
            //the names of two declarations never do
            result.Sort((a, b) => a.Start.CompareTo(b.Start));

            return result;
        }
    }
}
