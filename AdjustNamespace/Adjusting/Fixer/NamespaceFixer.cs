using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Fixer
{
    public class NamespaceFixer : IFixer
    {
        private readonly Workspace _workspace;
        private readonly string _symbolTargetNamespace;

        public string UniqueKey => _symbolTargetNamespace;

        public string OrderingKey => _symbolTargetNamespace;

        public NamespaceFixer(
            Workspace workspace,
            string symbolTargetNamespace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (symbolTargetNamespace is null)
            {
                throw new ArgumentNullException(nameof(symbolTargetNamespace));
            }
            _workspace = workspace;
            _symbolTargetNamespace = symbolTargetNamespace;
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
                var documentEditor = await _workspace.CreateDocumentEditorAsync(filePath);
                if (documentEditor == null)
                {
                    //skip this document
                    return;
                }

                var syntaxRoot = await documentEditor.OriginalDocument.GetSyntaxRootAsync();
                if (syntaxRoot == null)
                {
                    //skip this document
                    return;
                }

                var usingSyntaxes = syntaxRoot
                    .DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .ToList();


                if (usingSyntaxes.Count == 0)
                {
                    //no namespaces exists
                    //no need to insert new in this file
                    return;
                }

                if (usingSyntaxes.Any(s => s.Name.ToString() == _symbolTargetNamespace))
                {
                    //that using already exists
                    return;
                }

                var lastUsing = usingSyntaxes.Last();

                Debug.WriteLine($"Fix references in {documentEditor.OriginalDocument.FilePath}: '{lastUsing.Name}' -> '{_symbolTargetNamespace}' ");

                documentEditor.InsertAfter(
                    lastUsing,
                    SyntaxFactory.UsingDirective(
                        SyntaxFactory.ParseName(
                            " " + _symbolTargetNamespace
                            )
                        ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                    );

                var changedDocument = documentEditor.GetChangedDocument();
                r = _workspace.TryApplyChanges(changedDocument.Project.Solution);
            }
            while (!r);
        }
    }
}
