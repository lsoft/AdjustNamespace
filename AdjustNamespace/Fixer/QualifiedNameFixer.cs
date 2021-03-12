using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    public class QualifiedNameFixer : IFixer
    {
        private readonly QualifiedNameSyntax _qualifiedNameSyntax;
        private readonly string _symbolTargetNamespace;

        public string UniqueKey => _symbolTargetNamespace;

        public string OrderingKey => _symbolTargetNamespace;

        public QualifiedNameFixer(
            QualifiedNameSyntax qualifiedNameSyntax,
            string symbolTargetNamespace
            )
        {
            if (qualifiedNameSyntax is null)
            {
                throw new ArgumentNullException(nameof(qualifiedNameSyntax));
            }

            if (symbolTargetNamespace is null)
            {
                throw new ArgumentNullException(nameof(symbolTargetNamespace));
            }
            _qualifiedNameSyntax = qualifiedNameSyntax;
            _symbolTargetNamespace = symbolTargetNamespace;
        }

        public async Task FixAsync(DocumentEditor documentEditor)
        {
            if (documentEditor is null)
            {
                throw new ArgumentNullException(nameof(documentEditor));
            }

            documentEditor.ReplaceNode(
                _qualifiedNameSyntax,
                _qualifiedNameSyntax.WithLeft(SyntaxFactory.ParseName(" " + _symbolTargetNamespace))
                    .WithLeadingTrivia(_qualifiedNameSyntax.GetLeadingTrivia())
                    .WithTrailingTrivia(_qualifiedNameSyntax.GetTrailingTrivia())
                );
        }
    }
}
