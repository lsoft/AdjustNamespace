using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// A top level type the session is going to move to another namespace, as the search of
    /// the text references knows it. A nested type moves with its outer type and is never
    /// searched for on its own.
    /// </summary>
    [DebuggerDisplay("{OldFullName} -> {NewFullName}")]
    public sealed class MovedType
    {
        /// <summary>
        /// The namespace the type is moved out of.
        /// </summary>
        public string OldNamespace
        {
            get;
        }

        /// <summary>
        /// The namespace the type is moved into.
        /// </summary>
        public string NewNamespace
        {
            get;
        }

        /// <summary>
        /// The simple name of the type, without the arity.
        /// </summary>
        public string Name
        {
            get;
        }

        /// <summary>
        /// The number of the type parameters (0 for a type that is not generic).
        /// </summary>
        public int Arity
        {
            get;
        }

        /// <summary>
        /// The name of the assembly the type is declared in, or <c>null</c> if it is unknown.
        /// </summary>
        public string? AssemblyName
        {
            get;
        }

        /// <summary>
        /// The names of the types nested into this one, in the metadata form:
        /// <c>Inner</c>, <c>Inner+Deeper</c>.
        /// </summary>
        public IReadOnlyCollection<string> NestedTypeNames
        {
            get;
        }

        /// <summary>
        /// A type of that full name exists in another assembly the solution references, so a
        /// text that names it may mean that type.
        /// </summary>
        public bool HasLookalikeElsewhere
        {
            get;
        }

        /// <summary>
        /// The full name of the type before the move: <c>A.B.C</c>.
        /// </summary>
        public string OldFullName => OldNamespace + "." + Name;

        /// <summary>
        /// The full name of the type after the move.
        /// </summary>
        public string NewFullName => NewNamespace + "." + Name;

        /// <summary>
        /// The symbol of the type (the namespace state of the run is simulated with it).
        /// </summary>
        internal INamedTypeSymbol? Symbol
        {
            get;
        }

        public MovedType(
            string oldNamespace,
            string newNamespace,
            string name,
            int arity,
            string? assemblyName,
            IReadOnlyCollection<string>? nestedTypeNames = null,
            bool hasLookalikeElsewhere = false
            )
            : this(oldNamespace, newNamespace, name, arity, assemblyName, nestedTypeNames, hasLookalikeElsewhere, null)
        {
        }

        internal MovedType(
            string oldNamespace,
            string newNamespace,
            string name,
            int arity,
            string? assemblyName,
            IReadOnlyCollection<string>? nestedTypeNames,
            bool hasLookalikeElsewhere,
            INamedTypeSymbol? symbol
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

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            OldNamespace = oldNamespace;
            NewNamespace = newNamespace;
            Name = name;
            Arity = arity;
            AssemblyName = assemblyName;
            NestedTypeNames = nestedTypeNames ?? Array.Empty<string>();
            HasLookalikeElsewhere = hasLookalikeElsewhere;
            Symbol = symbol;
        }
    }
}
