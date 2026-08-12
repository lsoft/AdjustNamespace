using AdjustNamespace.Namespace;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Edit
{
    /// <summary>
    /// Everything an adjusting is going to change, grouped by file.
    ///
    /// The set is filled during the analysis and is applied afterwards, when the whole picture
    /// is known: a reference has to be fixed before the namespace of the file it points to
    /// is renamed, otherwise the reference is not found anymore.
    ///
    /// The set is a pure description of the decision. Nothing here touches the solution,
    /// so it may be built and asserted as it is.
    /// </summary>
    public sealed class EditSet
    {
        private readonly Dictionary<string, List<FileEdit>> _edits = new();

        /// <summary>
        /// The already added edits; the duplicates are dropped (the same file may reference
        /// the same moved type many times, and one <c>using</c> clause covers all of them).
        /// </summary>
        private readonly HashSet<FileEdit> _known = new();

        /// <summary>
        /// The files to be changed, in the order the edits of them have been added.
        /// </summary>
        private readonly List<string> _filePaths = new();

        /// <summary>
        /// The files to be changed, in the order the first edit of each of them has been added.
        /// </summary>
        public IReadOnlyList<string> FilePaths => _filePaths;

        /// <summary>
        /// There is nothing to change at all.
        /// </summary>
        public bool IsEmpty => _filePaths.Count == 0;

        /// <summary>
        /// The edits of the given file, in the order they have been added.
        /// </summary>
        /// <returns>An empty list if that file is no subject to change.</returns>
        public IReadOnlyList<FileEdit> EditsOf(string filePath)
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (!_edits.TryGetValue(filePath, out var fileEdits))
            {
                return Array.Empty<FileEdit>();
            }

            return fileEdits;
        }

        /// <summary>
        /// Schedule an edit. An edit which is scheduled already is ignored.
        /// </summary>
        public void Add(FileEdit edit)
        {
            if (edit is null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            if (!_known.Add(edit))
            {
                Debug.WriteLine($"[Adjust] EditSet: {edit} already scheduled, skipped");

                //this edit is scheduled already
                return;
            }

            Debug.WriteLine($"[Adjust] EditSet: scheduled {edit}");

            if (!_edits.TryGetValue(edit.FilePath, out var fileEdits))
            {
                fileEdits = new List<FileEdit>();
                _edits[edit.FilePath] = fileEdits;
                _filePaths.Add(edit.FilePath);
            }

            fileEdits.Add(edit);
        }

        /// <summary>
        /// Schedule a <c>using</c> clause of the given namespace to be imported by the file,
        /// see <see cref="AddUsingEdit"/>.
        /// </summary>
        public void AddUsing(string filePath, string namespaceName)
        {
            Add(new AddUsingEdit(filePath, namespaceName));
        }

        /// <summary>
        /// Schedule a span of the text of the file to be replaced,
        /// see <see cref="ReplaceTextEdit"/>.
        /// </summary>
        public void ReplaceText(string filePath, TextSpan span, string text)
        {
            Add(new ReplaceTextEdit(filePath, span, text));
        }

        /// <summary>
        /// Schedule a namespace of the file to be moved, see <see cref="MoveNamespaceEdit"/>.
        /// </summary>
        public void MoveNamespace(string filePath, NamespaceTransition transition)
        {
            Add(new MoveNamespaceEdit(filePath, transition));
        }
    }
}
