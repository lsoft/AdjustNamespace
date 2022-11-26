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
    /// </summary>
    public class NamespaceFixer : IFixer
    {
        private readonly VsServices _vss;
        private readonly List<NamespaceTransitionContainer> _subjectList = new();

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

        public void AddSubject(NamespaceTransitionContainer ntc)
        {
            _subjectList.Add(ntc);
        }

        public async Task FixAsync()
        {
            var workspace = _vss.Workspace;

            foreach (var ntc in _subjectList)
            {
                foreach (var transition in ntc.Transitions.Where(ni => ni.IsRoot))
                {
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

                        var cus = syntaxRoot as CompilationUnitSyntax;
                        if (cus == null)
                        {
                            //skip this namespace
                            break;
                        }
                        var newUsingStatement = SyntaxFactory.UsingDirective(
                            SyntaxFactory.ParseName(
                                " " + ufNamespace!.Name
                                )
                            ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

                        cus = cus.AddUsings(newUsingStatement);

                        if (!cus.TryFindNamespaceNodesFor(transition.OriginalName, out var fNamespaces))
                        {
                            //skip this namespace
                            break;
                        }

                        foreach (var fNamespace in fNamespaces!)
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

                            var fixedNamespace = fNamespace!.WithName(
                                newName
                                )
                                .WithLeadingTrivia(fNamespace.GetLeadingTrivia())
                                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                ;

                            cus = cus!.ReplaceNode(
                                fNamespace,
                                fixedNamespace
                                );
                        }

                        openedDocument = openedDocument.WithSyntaxRoot(cus!);

                        r = workspace.TryApplyChanges(openedDocument.Project.Solution);
                    }
                    while (!r);
                }
            }
        }
    }
}
