using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Fixer
{
    public class QualifiedNameFixer : IFixer
    {
        private readonly Workspace _workspace;
        private readonly List<QualifiedNameFixerArgument> _arguments = new ();

        public QualifiedNameFixer(
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

            if (!(o is QualifiedNameFixerArgument qnsa))
            {
                throw new Exception($"incorrect incoming type: {o.GetType()}");
            }

            _arguments.Add(qnsa);
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
                var document = _workspace.GetDocument(filePath);
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

                syntaxRoot = syntaxRoot.ReplaceNodes(
                    _arguments.ConvertAll(a => a.SourceSyntax),
                    (n0, n1) =>
                    {
                        var founda = _arguments.First(a => ReferenceEquals(a.SourceSyntax, n0));

                        return founda.ToReplaceSyntax;
                    });

                var changedDocument = document.WithSyntaxRoot(syntaxRoot);
                r = _workspace.TryApplyChanges(changedDocument.Project.Solution);
            }
            while (!r);

        }

        public class QualifiedNameFixerArgument
        {
            public SyntaxNode SourceSyntax
            {
                get;
            }
            public SyntaxNode ToReplaceSyntax
            {
                get;
            }

            public QualifiedNameFixerArgument(
                SyntaxNode qualifiedNameSyntax,
                SyntaxNode toReplaceSyntax
                )
            {
                if (qualifiedNameSyntax is null)
                {
                    throw new ArgumentNullException(nameof(qualifiedNameSyntax));
                }

                if (toReplaceSyntax is null)
                {
                    throw new ArgumentNullException(nameof(toReplaceSyntax));
                }

                SourceSyntax = qualifiedNameSyntax;
                ToReplaceSyntax = toReplaceSyntax;
            }
        }
    }
}
