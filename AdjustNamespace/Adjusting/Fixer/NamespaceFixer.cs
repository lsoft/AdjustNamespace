using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Fixer
{
    public class NamespaceFixer : IFixer
    {
        private readonly Workspace _workspace;
        private readonly HashSet<string> _symbolTargetNamespaces = new ();

        public NamespaceFixer(
            Workspace workspace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            _workspace = workspace;
        }

        public void AddSubject(object o)
        {
            if (o is null)
            {
                throw new ArgumentNullException(nameof(o));
            }

            if (!(o is string symbolTargetNamespace))
            {
                throw new Exception($"incorrect incoming type: {o.GetType()}");
            }

            _symbolTargetNamespaces.Add(symbolTargetNamespace);
        }


        public async Task FixAsync(string filePath)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            bool r;
            do
            {
                var document = _workspace.GetDocument(filePath)!;
                if (document == null)
                {
                    //skip this document
                    return;
                }

                var syntaxRoot = await document.GetSyntaxRootAsync();
                if (syntaxRoot == null)
                {
                    //skip this document
                    return;
                }

                foreach (var symbolTargetNamespace in _symbolTargetNamespaces)
                {
                    var usingSyntaxes = syntaxRoot
                        .DescendantNodes()
                        .OfType<UsingDirectiveSyntax>()
                        .ToList();


                    if (usingSyntaxes.Count == 0)
                    {
                        //no namespaces exists
                        //no need to insert new in this file
                        continue;
                    }

                    if (usingSyntaxes.Any(s => s.Name.ToString() == symbolTargetNamespace))
                    {
                        //that using already exists
                        continue;
                    }

                    var lastUsing = usingSyntaxes.Last();

                    Debug.WriteLine($"Fix references in {filePath}: '{lastUsing.Name}' -> '{symbolTargetNamespace}' ");

                    syntaxRoot = syntaxRoot.InsertNodesAfter(
                        lastUsing,
                        new[]
                        {
                            SyntaxFactory.UsingDirective(
                                SyntaxFactory.ParseName(
                                    " " + symbolTargetNamespace
                                    )
                                ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                        });

                }

                var changedDocument = document.WithSyntaxRoot(syntaxRoot);
                r = _workspace.TryApplyChanges(changedDocument.Project.Solution);
            }
            while (!r);
        }

    }
}
