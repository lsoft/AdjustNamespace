using System.Diagnostics;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// A place in a text where the full name of a moved type is written.
    /// </summary>
    [DebuggerDisplay("{Type.OldFullName} at {Start}")]
    internal sealed class NameOccurrence
    {
        /// <summary>
        /// The moved type that is named.
        /// </summary>
        public MovedType Type
        {
            get;
        }

        /// <summary>
        /// Where the name <c>A.B.C</c> starts in the text that has been searched.
        /// </summary>
        public int Start
        {
            get;
        }

        /// <summary>
        /// The length of the name: <c>A.B.C</c> only, without a <c>global::</c> in front and
        /// without the metadata form and the assembly behind.
        /// </summary>
        public int Length
        {
            get;
        }

        /// <summary>
        /// Where the whole occurrence starts: at the <c>global::</c> if there is one.
        /// </summary>
        public int FullStart
        {
            get;
        }

        /// <summary>
        /// Where the whole occurrence ends: behind the metadata form and the assembly.
        /// </summary>
        public int FullEnd
        {
            get;
        }

        /// <summary>
        /// The arity written as <c>`N</c> behind the name, or -1.
        /// </summary>
        public int WrittenArity
        {
            get;
        }

        /// <summary>
        /// The nested types written as <c>+Inner+Deeper</c> behind the name, without the first
        /// <c>+</c>: <c>Inner+Deeper</c>. Empty if there is none.
        /// </summary>
        public string WrittenNestedPath
        {
            get;
        }

        /// <summary>
        /// The assembly written behind the name (<c>A.B.C, MyAsm</c>), or <c>null</c>.
        /// </summary>
        public string? WrittenAssembly
        {
            get;
        }

        /// <summary>
        /// What follows the moved name in the dotted token: <c>.Member.Other</c>, or empty.
        /// </summary>
        public string DottedRemainder
        {
            get;
        }

        public NameOccurrence(
            MovedType type,
            int start,
            int length,
            int fullStart,
            int fullEnd,
            int writtenArity,
            string writtenNestedPath,
            string? writtenAssembly,
            string dottedRemainder
            )
        {
            Type = type;
            Start = start;
            Length = length;
            FullStart = fullStart;
            FullEnd = fullEnd;
            WrittenArity = writtenArity;
            WrittenNestedPath = writtenNestedPath;
            WrittenAssembly = writtenAssembly;
            DottedRemainder = dottedRemainder;
        }
    }
}
