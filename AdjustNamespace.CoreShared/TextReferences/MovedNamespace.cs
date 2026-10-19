using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// A namespace the session takes types out of. A text names a namespace in a few places
    /// only (a Razor <c>@using</c>, a <c>GeneratedClassNamespace</c>), and it is changed when the
    /// namespace is gone after the run and all of its types went to the same place.
    /// </summary>
    [DebuggerDisplay("{Name} -> {NewName}")]
    public sealed class MovedNamespace
    {
        /// <summary>
        /// The namespace the types leave.
        /// </summary>
        public string Name
        {
            get;
        }

        /// <summary>
        /// The namespaces the types go to.
        /// </summary>
        public IReadOnlyCollection<string> Targets
        {
            get;
        }

        /// <summary>
        /// No type is left in the namespace after the run.
        /// </summary>
        public bool IsEmptied
        {
            get;
        }

        /// <summary>
        /// Another referenced assembly declares the namespace as well.
        /// </summary>
        public bool HasLookalikeElsewhere
        {
            get;
        }

        /// <summary>
        /// The only namespace all the types go to, or <c>null</c> if they go to several ones.
        /// </summary>
        public string? NewName => Targets.Count == 1 ? Targets.First() : null;

        public MovedNamespace(
            string name,
            IReadOnlyCollection<string> targets,
            bool isEmptied,
            bool hasLookalikeElsewhere = false
            )
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (targets is null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            Name = name;
            Targets = targets;
            IsEmptied = isEmptied;
            HasLookalikeElsewhere = hasLookalikeElsewhere;
        }
    }
}
