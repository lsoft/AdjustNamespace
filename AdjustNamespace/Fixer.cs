using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    public interface IFixer
    {
        string UniqueKey
        {
            get;
        }

        string OrderingKey
        {
            get;
        }

        Task FixAsync(DocumentEditor documentEditor);
    }

    public class FixerEqualityComparer : IEqualityComparer<IFixer>
    {
        public static readonly FixerEqualityComparer Entity = new FixerEqualityComparer();

        public bool Equals(IFixer x, IFixer y)
        {
            if (x == null && y == null)
            {
                return true;
            }
            if (x != null && y == null)
            {
                return false;
            }
            if (x == null && y != null)
            {
                return false;
            }

            if (x!.GetType() != y!.GetType())
            {
                return false;
            }

            return x.UniqueKey.Equals(y.UniqueKey);
        }

        public int GetHashCode(IFixer obj)
        {
            return HashCode.Combine(
                obj?.GetType().GetHashCode() ?? 0,
                obj?.UniqueKey.GetHashCode() ?? 0
                );
        }
    }

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
