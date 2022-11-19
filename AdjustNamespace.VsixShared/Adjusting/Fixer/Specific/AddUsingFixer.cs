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
    /// <summary>
    /// Fixer for adding a using statements like <code>using System.Threading.Tasks</code>
    /// </summary>
    public class AddUsingFixer : IFixer
    {
        private readonly Workspace _workspace;
        private readonly HashSet<string> _symbolTargetNamespaces = new ();

        public string FilePath
        {
            get;
        }

        public AddUsingFixer(
            Workspace workspace,
            string filePath
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

            _workspace = workspace;
            FilePath = filePath;
        }


        public void AddSubject(string symbolTargetNamespace)
        {
            if (symbolTargetNamespace is null)
            {
                throw new ArgumentNullException(nameof(symbolTargetNamespace));
            }

            _symbolTargetNamespaces.Add(symbolTargetNamespace);
        }


        public async Task FixAsync()
        {
            bool r;
            do
            {
                var (document, syntaxRoot) = await _workspace.GetDocumentAndSyntaxRootAsync(FilePath);
                if (document == null || syntaxRoot == null)
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
                        //no using namespaces exists
                        //no need to insert new in this file
                        continue;
                    }

                    if (usingSyntaxes.Any(s => s.Name.ToString() == symbolTargetNamespace))
                    {
                        //that using already exists
                        continue;
                    }

                    var lastUsing = usingSyntaxes.Last();

                    Debug.WriteLine($"Fix references in {FilePath}: '{lastUsing.Name}' -> '{symbolTargetNamespace}' ");

                    syntaxRoot = syntaxRoot.InsertNodesAfter(
                        lastUsing,
                        new[]
                        {
                            SyntaxFactory.UsingDirective(
                                SyntaxFactory.ParseName(
                                    " " + symbolTargetNamespace
                                    )
                                ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                                .WithLeadingTrivia(lastUsing.GetLeadingTrivia())
                        });

                }

                var changedDocument = document.WithSyntaxRoot(syntaxRoot);
                r = _workspace.TryApplyChanges(changedDocument.Project.Solution);
            }
            while (!r);
        }
    }
}
