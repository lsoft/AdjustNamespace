using System.IO;

namespace AdjustNamespace.Cli
{
    /// <summary>
    /// Shortening of the paths for the output: a full path of every file makes the report of
    /// a big solution unreadable.
    /// </summary>
    public static class RelativePath
    {
        /// <summary>
        /// The given path relative to the given folder, or the path itself if it lies
        /// outside of that folder (a linked file, for example).
        /// </summary>
        public static string Of(
            string filePath,
            string rootFolder
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (rootFolder is null)
            {
                throw new ArgumentNullException(nameof(rootFolder));
            }

            var folder = rootFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return filePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
                ? filePath.Substring(folder.Length)
                : filePath
                ;
        }
    }
}
