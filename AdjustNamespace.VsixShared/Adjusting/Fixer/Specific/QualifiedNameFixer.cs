using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Fixer
{
    /// <summary>
    /// Fixer for fully qualified names like <code>AdjustNamespace.Adjusting.Fixer.QualifiedNameFixer</code>
    /// </summary>
    public class QualifiedNameFixer : IFixer
    {
        private readonly Workspace _workspace;
        private readonly List<QualifiedNameFixerArgument> _arguments = new ();

        public string FilePath
        {
            get;
        }

        public QualifiedNameFixer(
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

        public void AddSubject(QualifiedNameFixerArgument qnsa)
        {
            if (qnsa is null)
            {
                throw new ArgumentNullException(nameof(qnsa));
            }

            _arguments.Add(qnsa);
        }

        public async Task FixAsync()
        {
            await _workspace.ApplyModifiedDocumentAsync(
                FilePath,
                (document, syntaxRoot) =>
                {
                    var mSyntaxRoot = syntaxRoot.ReplaceNodes(
                        _arguments.ConvertAll(a => a.SourceSyntax),
                        (n0, n1) =>
                        {
                            var founda = _arguments.First(a => ReferenceEquals(a.SourceSyntax, n0));

                            return founda.ToReplaceSyntax;
                        });

                    var changedDocument = document.WithSyntaxRoot(mSyntaxRoot);
                    return changedDocument;
                }
                );
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
