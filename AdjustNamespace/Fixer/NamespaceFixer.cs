using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    public class NamespaceFixer : IFixer
    {
        private readonly string _symbolTargetNamespace;

        public string UniqueKey => _symbolTargetNamespace;

        public string OrderingKey => _symbolTargetNamespace;

        public NamespaceFixer(
            string symbolTargetNamespace
            )
        {
            if (symbolTargetNamespace is null)
            {
                throw new ArgumentNullException(nameof(symbolTargetNamespace));
            }

            _symbolTargetNamespace = symbolTargetNamespace;
        }

        public async Task FixAsync(DocumentEditor documentEditor)
        {
            if (documentEditor is null)
            {
                throw new ArgumentNullException(nameof(documentEditor));
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

            documentEditor.InsertAfter(
                usingSyntaxes.Last(),
                SyntaxFactory.UsingDirective(
                    SyntaxFactory.ParseName(
                        " " + _symbolTargetNamespace
                        )
                    ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                );
        }
    }
}
