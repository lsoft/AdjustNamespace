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

        /// <summary>
        /// Nodes to be replaced (identified by their span in the original document)
        /// and their replacements.
        /// </summary>
        private readonly List<QualifiedNameFixerArgument> _arguments = new ();

        /// <inheritdoc/>
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

        /// <summary>
        /// Schedule a replacement of a fully qualified name.
        /// </summary>
        public void AddSubject(QualifiedNameFixerArgument qnsa)
        {
            _arguments.Add(qnsa);
        }

        /// <inheritdoc/>
        public async Task FixAsync()
        {
            await _workspace.ApplyModifiedDocumentAsync(
                FilePath,
                (document, syntaxRoot) =>
                {
                    var changes = BuildChanges();
                    if (changes.Count == 0)
                    {
                        //nothing to do
                        return null;
                    }

                    var changedDocument = document.WithText(
                        syntaxRoot.GetText().WithChanges(changes)
                        );
                    return changedDocument;
                }
                );
        }

        /// <summary>
        /// The scheduled replacements as the changes of the text of the file.
        ///
        /// A name is replaced by its span and not by its syntax node: a file which several
        /// projects compile has a syntax tree per project, the references are found in all
        /// of them, and a name which is guarded by a conditional compilation symbol is
        /// a disabled text (i.e. a trivia and not a name) in a part of these trees.
        /// The text is the same for all of them, so a span is valid everywhere.
        /// </summary>
        private List<TextChange> BuildChanges()
        {
            //`SourceText.WithChanges` does not accept the intersecting changes, while
            //a nested name may well be scheduled twice (`A.B.Outer.Inner` is a reference
            //to `Outer` and a reference to `Inner` at once). The longest one wins,
            //exactly as it does when the syntax nodes are replaced.
            var ordered = _arguments
                .OrderBy(a => a.SubjectNodeSpan.Start)
                .ThenByDescending(a => a.SubjectNodeSpan.Length)
                .ToList();

            var result = new List<TextChange>(ordered.Count);

            var lastEnd = -1;
            foreach (var argument in ordered)
            {
                if (argument.SubjectNodeSpan.Start < lastEnd)
                {
                    //inside of (or intersecting with) an already scheduled replacement
                    continue;
                }

                result.Add(
                    new TextChange(
                        argument.SubjectNodeSpan,
                        argument.ToReplaceSyntax.ToString()
                        )
                    );

                lastEnd = argument.SubjectNodeSpan.End;
            }

            return result;
        }

        /// <summary>
        /// A single replacement: what to replace (a span in the document) and what to replace it with.
        /// </summary>
        public readonly struct QualifiedNameFixerArgument
        {
            /// <summary>
            /// Span of the node to be replaced.
            /// </summary>
            public readonly TextSpan SubjectNodeSpan;

            /// <summary>
            /// New syntax node to be placed instead of the node above.
            /// </summary>
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
