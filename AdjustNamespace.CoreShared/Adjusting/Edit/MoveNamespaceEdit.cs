using AdjustNamespace.Namespace;
using System.Diagnostics;

namespace AdjustNamespace.Adjusting.Edit
{
    /// <summary>
    /// A namespace declared in the file has to be moved: every declaration of it gets
    /// the new name, and the old name is imported with a <c>using</c> clause (the file may
    /// reference the types which stay in that namespace).
    ///
    /// The nested namespaces are not mentioned here: their full name contains the name of
    /// the root one and they are moved with it, so a root transition is the only subject.
    /// </summary>
    [DebuggerDisplay("{FilePath}: {Transition.OriginalName} -> {Transition.ModifiedName}")]
    public sealed class MoveNamespaceEdit : FileEdit
    {
        /// <summary>
        /// Old namespace name -> new namespace name.
        /// </summary>
        public NamespaceTransition Transition
        {
            get;
        }

        public MoveNamespaceEdit(
            string filePath,
            NamespaceTransition transition
            )
            : base(filePath)
        {
            Transition = transition;
        }

        public override bool Equals(object? obj)
        {
            return obj is MoveNamespaceEdit other
                && other.FilePath == FilePath
                && other.Transition.OriginalName == Transition.OriginalName
                && other.Transition.ModifiedName == Transition.ModifiedName
                ;
        }

        public override int GetHashCode()
        {
            return FilePath.GetHashCode() ^ Transition.OriginalName.GetHashCode() ^ Transition.ModifiedName.GetHashCode();
        }

        public override string ToString()
        {
            return $"{FilePath}: namespace {Transition.OriginalName} -> {Transition.ModifiedName}";
        }
    }
}
