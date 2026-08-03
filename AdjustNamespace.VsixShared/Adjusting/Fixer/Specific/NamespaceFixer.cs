using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AdjustNamespace.Helper;
using System.Linq;
using Microsoft.CodeAnalysis;
using AdjustNamespace.Namespace;

namespace AdjustNamespace.Adjusting.Fixer.Specific
{
    /// <summary>
    /// A fixer for editing namespace clauses to the correct one.
    /// Both the classic (<c>namespace A { }</c>) and the file scoped (<c>namespace A;</c>)
    /// declarations are supported.
    /// </summary>
    public class NamespaceFixer : IFixer
    {
        private readonly VsServices _vss;

        /// <summary>
        /// Namespace transitions to be applied to the file.
        /// </summary>
        private readonly List<NamespaceTransitionContainer> _subjectList = new();

        /// <inheritdoc/>
        public string FilePath
        {
            get;
        }

        public NamespaceFixer(
            VsServices vss,
            string filePath
            )
        {
            _vss = vss;
            FilePath = filePath;
        }

        /// <summary>
        /// Schedule the namespace transitions to be applied to the file.
        /// </summary>
        public void AddSubject(NamespaceTransitionContainer ntc)
        {
            _subjectList.Add(ntc);
        }

        /// <summary>
        /// The file imports the given namespace already.
        /// <c>using Alias = A.B;</c> and <c>using static A.B;</c> do not import it,
        /// see <see cref="AddUsingFixer"/>.
        /// </summary>
        private static bool HasUsingOf(CompilationUnitSyntax cus, string namespaceName)
        {
            return cus
                .GetAllDescendants<UsingDirectiveSyntax>()
                .Any(s =>
                    s.Alias == null
                    && s.StaticKeyword.IsKind(SyntaxKind.None)
                    && s.Name.ToString() == namespaceName
                    );
        }

        /// <summary>
        /// The namespace the file is being moved out of still contains something for the project
        /// of that file, i.e. a <c>using</c> clause of it compiles after the move.
        /// The types of the file itself do not count: all of them are moving.
        /// </summary>
        private async Task<bool> IsAliveInItsProjectAsync(
            Document document,
            string namespaceName
            )
        {
            var compilation = await document.Project.GetCompilationAsync();
            if (compilation == null)
            {
                //we know nothing, so keep the old behaviour
                return true;
            }

            return compilation.IsNamespaceFilledOutside(namespaceName, FilePath);
        }

        /// <inheritdoc/>
        public async Task FixAsync()
        {
            var workspace = _vss.Workspace;

            foreach (var ntc in _subjectList)
            {
                //only the root namespaces are renamed; the nested ones are renamed automatically
                //because their full name contains the root one
                foreach (var transition in ntc.Transitions.Where(ni => ni.IsRoot))
                {
                    //see the comment in AddUsingFixer.FixAsync about this do-while
                    bool r = true;
                    do
                    {
                        var (openedDocument, syntaxRoot) = await workspace.GetDocumentAndSyntaxRootAsync(FilePath);
                        if (openedDocument == null || syntaxRoot == null)
                        {
                            //something went wrong
                            //skip this document
                            return;
                        }

                        if (!syntaxRoot.TryFindNamespaceNodesFor(transition.OriginalName, out var ufNamespaces))
                        {
                            //skip this namespace
                            break;
                        }

                        var ufNamespace = ufNamespaces.First();

                        //class A : IA {}
                        //we're moving A into a different namespace, but IA are not.
                        //we need to insert 'using old namespace'
                        //otherwise ia will not be resolved

                        //we can't determite it is the case or it's not without a costly analysis
                        //it's a subject for a future work
                        //so add at 100% cases now

                        //...with a single exception: if this project has nothing in that
                        //namespace anymore, such a clause does not compile. The namespace may
                        //stay alive for the solution and be gone for this project at the same
                        //time (another project fills it and this one does not reference it),
                        //and then the cleanup has no reason to remove the clause again.

                        var cus = syntaxRoot as CompilationUnitSyntax;
                        if (cus == null)
                        {
                            //skip this namespace
                            break;
                        }
                        if (!HasUsingOf(cus, ufNamespace!.Name.ToString())
                            && await IsAliveInItsProjectAsync(openedDocument, ufNamespace!.Name.ToString())
                            )
                        {
                            var newUsingStatement = SyntaxFactory.UsingDirective(
                                SyntaxFactory.ParseName(
                                    " " + ufNamespace!.Name
                                    )
                                ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                            cus = cus.AddUsingKeepingHeader(newUsingStatement);
                        }

                        if (!cus.TryFindNamespaceNodesFor(transition.OriginalName, out var fNamespaces))
                        {
                            //skip this namespace
                            break;
                        }

                        //all the declarations are replaced at once: a node found in `cus`
                        //does not belong to the tree produced by ReplaceNode anymore,
                        //so replacing them one by one silently skips all of them but the first
                        cus = cus!.ReplaceNodes(
                            fNamespaces!,
                            (fNamespace, _) =>
                            {
                                var newName = SyntaxFactory.ParseName(
                                    transition.ModifiedName
                                    );

                                if (fNamespace is NamespaceDeclarationSyntax)
                                {
                                    newName = newName
                                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                        ;
                                }

                                return fNamespace.WithName(
                                    newName
                                    )
                                    .WithLeadingTrivia(fNamespace.GetLeadingTrivia())
                                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                    ;
                            });

                        openedDocument = openedDocument.WithSyntaxRoot(cus!);

                        r = workspace.TryApplyChanges(openedDocument.Project.Solution);
                    }
                    while (!r);
                }
            }
        }
    }
}
