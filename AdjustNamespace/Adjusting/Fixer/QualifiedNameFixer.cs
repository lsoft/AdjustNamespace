using AdjustNamespace.Helper;
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
        private readonly Workspace _workspace;
        private readonly QualifiedNameSyntax _qualifiedNameSyntax;
        private readonly string _symbolTargetNamespace;

        public string UniqueKey => _symbolTargetNamespace;

        public string OrderingKey => _symbolTargetNamespace;

        public QualifiedNameFixer(
            Workspace workspace,
            QualifiedNameSyntax qualifiedNameSyntax,
            string symbolTargetNamespace
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (qualifiedNameSyntax is null)
            {
                throw new ArgumentNullException(nameof(qualifiedNameSyntax));
            }

            if (symbolTargetNamespace is null)
            {
                throw new ArgumentNullException(nameof(symbolTargetNamespace));
            }
            _workspace = workspace;
            _qualifiedNameSyntax = qualifiedNameSyntax;
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

                documentEditor.ReplaceNode(
                    _qualifiedNameSyntax,
                    _qualifiedNameSyntax.WithLeft(SyntaxFactory.ParseName(" " + _symbolTargetNamespace))
                        .WithLeadingTrivia(_qualifiedNameSyntax.GetLeadingTrivia())
                        .WithTrailingTrivia(_qualifiedNameSyntax.GetTrailingTrivia())
                    );

                var changedDocument = documentEditor.GetChangedDocument();
                r = _workspace.TryApplyChanges(changedDocument.Project.Solution);
            }
            while (!r);

        }
    }
}
