using System;
using System.Diagnostics;

namespace AdjustNamespace.VisualStudio
{
    /// <summary>
    /// A project of the solution, as everything outside of the Visual Studio layer knows it.
    ///
    /// The solution tree of Visual Studio describes a project with a <c>SolutionItem</c> which
    /// is available from the main thread only; the rest of the extension needs two strings of
    /// it and nothing more, so <see cref="ISolutionExplorer"/> hands out this value instead.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public readonly struct ProjectRef
    {
        /// <summary>
        /// Name of the project (<c>MyApp.Shared</c>).
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// Full path to the project file (<c>...\MyApp.Shared\MyApp.Shared.csproj</c>).
        /// </summary>
        public readonly string FilePath;

        public ProjectRef(
            string name,
            string filePath
            )
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            Name = name;
            FilePath = filePath;
        }
    }
}
