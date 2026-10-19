using AdjustNamespace.Adjusting.Edit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// A file of the solution a run has changed, with the kinds of the changes.
    /// </summary>
    [DebuggerDisplay("{FilePath}")]
    public sealed class AdjustTouchedFile
    {
        /// <summary>
        /// Full path to the file.
        /// </summary>
        public string FilePath
        {
            get;
        }

        /// <summary>
        /// How many edits of every kind the file has got, in the order of <see cref="EditKind"/>.
        /// </summary>
        public IReadOnlyList<KeyValuePair<EditKind, int>> Edits
        {
            get;
        }

        /// <summary>
        /// The number of all the edits of the file.
        /// </summary>
        public int TotalEdits => Edits.Sum(e => e.Value);

        public AdjustTouchedFile(
            string filePath,
            IReadOnlyDictionary<EditKind, int> edits
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (edits is null)
            {
                throw new ArgumentNullException(nameof(edits));
            }

            FilePath = filePath;
            Edits = edits.OrderBy(e => e.Key).ToList();
        }

        /// <summary>
        /// The edits as a phrase: <c>using added x2, name qualified</c>.
        /// </summary>
        public string DescribeEdits()
        {
            return string.Join(
                ", ",
                Edits.Select(e => e.Value == 1
                    ? EditKinds.Describe(e.Key)
                    : $"{EditKinds.Describe(e.Key)} x{e.Value}")
                );
        }
    }
}
