using System;
using System.Collections.Generic;
using System.Text;

namespace AdjustNamespace.Namespace
{
    /// <summary>
    /// A single namespace transition: where the types of a namespace live now
    /// and where they should live after the adjusting.
    /// </summary>
    public readonly struct NamespaceTransition
    {
        /// <summary>
        /// Original namespace name.
        /// </summary>
        public readonly string OriginalName;

        /// <summary>
        /// Desired (target) namespace name.
        /// (namespace name bringed in accordance with its file folders)
        /// </summary>
        public readonly string ModifiedName;

        /// <summary>
        /// Is this original namespace is root in the document.
        /// </summary>
        public readonly bool IsRoot;

        /// <param name="originalName">Original namespace name.</param>
        /// <param name="modifiedName">Desired (target) namespace name.</param>
        /// <param name="isRoot">Is this original namespace is root in the document.</param>
        public NamespaceTransition(
            string originalName,
            string modifiedName,
            bool isRoot
            )
        {
            if (originalName is null)
            {
                throw new ArgumentNullException(nameof(originalName));
            }

            if (modifiedName is null)
            {
                throw new ArgumentNullException(nameof(modifiedName));
            }

            OriginalName = originalName;
            ModifiedName = modifiedName;
            IsRoot = isRoot;
        }
    }
}
