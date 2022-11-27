using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
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
            _arguments.Add(qnsa);
        }

        public async Task FixAsync()
        {
            await _workspace.ApplyModifiedDocumentAsync(
                FilePath,
                (document, syntaxRoot) =>
                {
                    var nodesToBeReplaced = _arguments.ConvertAll(
                        a => syntaxRoot.FindNode(a.SubjectNodeSpan).GoDownTo(a.ToReplaceSyntax.GetType())!
                        );

                    var mSyntaxRoot = syntaxRoot.ReplaceNodes(
                        nodesToBeReplaced,
                        (n0, n1) =>
                        {
                            var founda = _arguments.First(a => n0.Span == a.SubjectNodeSpan);

                            return founda.ToReplaceSyntax;
                        });

                    var changedDocument = document.WithSyntaxRoot(mSyntaxRoot);
                    return changedDocument;
                }
                );
        }

        public readonly struct QualifiedNameFixerArgument
        {
            public readonly TextSpan SubjectNodeSpan;
            public readonly SyntaxNode ToReplaceSyntax;

            public QualifiedNameFixerArgument(
                TextSpan subjectNodeSpan,
                SyntaxNode toReplaceSyntax
                )
            {
                if (toReplaceSyntax is null)
                {
                    throw new ArgumentNullException(nameof(toReplaceSyntax));
                }

                SubjectNodeSpan = subjectNodeSpan;
                ToReplaceSyntax = toReplaceSyntax;
            }
        }
    }
}
