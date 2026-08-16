using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AdjustNamespace.Cli
{
    /// <summary>
    /// The <c>--path</c> options: which files of the solution the user has asked to adjust.
    ///
    /// This is the equivalent of the selection in the solution explorer: the extension adjusts
    /// the files of the chosen items, and an empty selection here means the whole solution.
    /// </summary>
    public sealed class PathFilter
    {
        private readonly IReadOnlyList<string> _roots;

        /// <summary>
        /// The given paths which match no file of the solution at all. A typo in a path would
        /// otherwise silently adjust nothing.
        /// </summary>
        private readonly HashSet<string> _unmatchedRoots;

        /// <param name="roots">Full paths of the chosen files and folders.</param>
        public PathFilter(
            IReadOnlyList<string> roots
            )
        {
            if (roots is null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            _roots = roots;
            _unmatchedRoots = new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The paths which have matched nothing, see <see cref="Matches"/>.
        /// </summary>
        public IReadOnlyList<string> UnmatchedRoots => _roots.Where(_unmatchedRoots.Contains).ToList();

        /// <summary>
        /// Is this file chosen by the user?
        /// </summary>
        public bool Matches(
            string filePath
            )
        {
            if (_roots.Count == 0)
            {
                return true;
            }

            var matched = false;
            foreach (var root in _roots)
            {
                if (!IsUnder(filePath, root))
                {
                    continue;
                }

                _unmatchedRoots.Remove(root);
                matched = true;
            }

            return matched;
        }

        private static bool IsUnder(
            string filePath,
            string root
            )
        {
            if (string.Equals(filePath, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var folder = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return filePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        }
    }
}
