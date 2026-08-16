using System;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Edit
{
    /// <summary>
    /// A <c>using</c> clause of the given namespace has to be imported by the file:
    /// the file references a type which is being moved into that namespace.
    ///
    /// A clause which the file has already is not written twice, and it is the applier who
    /// knows it: the analysis has no idea what the file looks like at the moment its edits
    /// are applied.
    /// </summary>
    [DebuggerDisplay("{FilePath}: using {NamespaceName}")]
    public sealed class AddUsingEdit : FileEdit
    {
        /// <summary>
        /// The namespace to import.
        /// </summary>
        public string NamespaceName
        {
            get;
        }

        public AddUsingEdit(
            string filePath,
            string namespaceName
            )
            : base(filePath)
        {
            if (namespaceName is null)
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }

            NamespaceName = namespaceName;
        }

        public override bool Equals(object? obj)
        {
            return obj is AddUsingEdit other
                && other.FilePath == FilePath
                && other.NamespaceName == NamespaceName
                ;
        }

        public override int GetHashCode()
        {
            return FilePath.GetHashCode() ^ NamespaceName.GetHashCode();
        }

        public override string ToString()
        {
            return $"{FilePath}: using {NamespaceName};";
        }
    }
}
