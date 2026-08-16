using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdjustNamespace;

namespace AdjustNamespace.Adjusting.Edit.Apply
{
    /// <summary>
    /// Applies the <see cref="AddUsingEdit"/> of a file: all of them at once, as a single
    /// change of the syntax tree of the document.
    /// </summary>
    public static class AddUsingApplier
    {
        public static async Task ApplyAsync(
            Workspace workspace,
            string filePath,
            IReadOnlyList<AddUsingEdit> edits
            )
        {
            if (edits.Count == 0)
            {
                return;
            }

            await workspace.ApplyModifiedDocumentAsync(
                filePath,
                (document, syntaxRoot) =>
                {
                    foreach (var edit in edits)
                    {
                        syntaxRoot = AddUsing(filePath, syntaxRoot, edit.NamespaceName);
                    }

                    return document.WithSyntaxRoot(syntaxRoot);
                }
                );
        }

        /// <summary>
        /// Write a new <c>using</c> clause into the syntax tree of the document,
        /// unless the file imports that namespace already.
        /// </summary>
        private static SyntaxNode AddUsing(
            string filePath,
            SyntaxNode syntaxRoot,
            string symbolTargetNamespace
            )
        {
            //only the clauses of the file itself are looked at, and a new one is placed
            //among them: a clause which is written inside a namespace declaration is
            //visible in that namespace only (so it neither makes the new one redundant
            //nor is a place for it), and its name is resolved relatively to that
            //namespace (so `using X.Y;` inside `namespace Some.X` is not `X.Y` at all)
            var cus = (CompilationUnitSyntax)syntaxRoot;

            var usingSyntaxes = cus.Usings.ToList();

            if (usingSyntaxes.Count > 0)
            {
                //`using Alias = A.B;` and `using static A.B;` do not import the namespace,
                //so they do not make the new using clause redundant
                if (usingSyntaxes.Any(s =>
                    s.Alias == null
                    && s.StaticKeyword.IsKind(SyntaxKind.None)
                    //Name is nullable since Roslyn 5 (`using unsafe int*;` has no name)
                    && s.Name?.ToString() == symbolTargetNamespace
                    ))
                {
                    //that using already exists
                    return syntaxRoot;
                }
            }

            AdjustLog.WriteLine($"Fix references in {filePath}: Add '{symbolTargetNamespace}' ");

            if (usingSyntaxes.Count > 0)
            {
                //put the new clause after the existing ones to preserve their order
                var lastUsing = usingSyntaxes.Last();

                return syntaxRoot.InsertNodesAfter(
                    lastUsing,
                    new[]
                    {
                        SyntaxFactory.UsingDirective(
                            SyntaxFactory.ParseName(
                                " " + symbolTargetNamespace
                                )
                            ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                            .WithLeadingTrivia(lastUsing.GetIndentationOf())
                    });
            }

            //there are no using clauses in this file at all
            return cus.AddUsingKeepingHeader(
                SyntaxFactory.UsingDirective(
                    SyntaxFactory.ParseName(
                        " " + symbolTargetNamespace
                        )
                    ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.CarriageReturnLineFeed)
                    );
        }
    }
}
