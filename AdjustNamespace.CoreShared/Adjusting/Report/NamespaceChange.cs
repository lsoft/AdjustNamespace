using System;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// A namespace of a file and the namespace it has been moved into.
    /// </summary>
    [DebuggerDisplay("{Old} -> {New}")]
    public readonly struct NamespaceChange
    {
        /// <summary>
        /// The namespace before the run.
        /// </summary>
        public string Old
        {
            get;
        }

        /// <summary>
        /// The namespace after the run.
        /// </summary>
        public string New
        {
            get;
        }

        public NamespaceChange(
            string oldNamespace,
            string newNamespace
            )
        {
            if (oldNamespace is null)
            {
                throw new ArgumentNullException(nameof(oldNamespace));
            }

            if (newNamespace is null)
            {
                throw new ArgumentNullException(nameof(newNamespace));
            }

            Old = oldNamespace;
            New = newNamespace;
        }

        public override string ToString()
        {
            return $"{Display(Old)} -> {Display(New)}";
        }

        private static string Display(string name)
        {
            return name.Length == 0 ? "(global namespace)" : name;
        }
    }
}
