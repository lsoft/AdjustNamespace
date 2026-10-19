using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// The types (and the namespaces) a session moves, indexed by the names a text can mention.
    /// This is all the search needs to know about the session: it never looks at the
    /// solution again.
    /// </summary>
    public sealed class MovedTypeSet
    {
        private readonly Dictionary<string, List<MovedType>> _typesByOldFullName =
            new(StringComparer.Ordinal);

        private readonly Dictionary<string, MovedNamespace> _namespacesByName =
            new(StringComparer.Ordinal);

        /// <summary>
        /// The moved types.
        /// </summary>
        public IReadOnlyList<MovedType> Types
        {
            get;
        }

        /// <summary>
        /// The namespaces the moved types leave.
        /// </summary>
        public IReadOnlyList<MovedNamespace> Namespaces
        {
            get;
        }

        /// <summary>
        /// There is nothing moved, so there is nothing to search for.
        /// </summary>
        public bool IsEmpty => Types.Count == 0;

        public MovedTypeSet(
            IReadOnlyList<MovedType> types,
            IReadOnlyList<MovedNamespace> namespaces
            )
        {
            if (types is null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            if (namespaces is null)
            {
                throw new ArgumentNullException(nameof(namespaces));
            }

            Types = types;
            Namespaces = namespaces;

            foreach (var type in types)
            {
                if (!_typesByOldFullName.TryGetValue(type.OldFullName, out var list))
                {
                    list = new List<MovedType>();
                    _typesByOldFullName[type.OldFullName] = list;
                }

                list.Add(type);
            }

            foreach (var @namespace in namespaces)
            {
                _namespacesByName[@namespace.Name] = @namespace;
            }
        }

        /// <summary>
        /// The moved types of that old full name (several ones, if the types differ by the arity).
        /// </summary>
        public IReadOnlyList<MovedType> TypesOf(string oldFullName)
        {
            return _typesByOldFullName.TryGetValue(oldFullName, out var list)
                ? list
                : Array.Empty<MovedType>();
        }

        /// <summary>
        /// The moved namespace of that name, or <c>null</c>.
        /// </summary>
        public MovedNamespace? NamespaceOf(string name)
        {
            return _namespacesByName.TryGetValue(name, out var result) ? result : null;
        }

        /// <summary>
        /// The text is the old full name of a moved type, or may start with one.
        /// </summary>
        internal bool IsKnownFullName(string name)
        {
            return _typesByOldFullName.ContainsKey(name);
        }
    }
}
