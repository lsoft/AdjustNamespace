using System;
using System.IO;

namespace AdjustNamespace.Adjusting.Report
{
    /// <summary>
    /// Shortening of the paths for the report: a full path of every file makes the report of
    /// a big solution unreadable.
    /// </summary>
    public static class ReportPaths
    {
        /// <summary>
        /// The given path relative to the given folder, or the path itself if there is no folder
        /// or the path lies outside of it.
        /// </summary>
        public static string Relative(
            string filePath,
            string? rootFolder
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (string.IsNullOrEmpty(rootFolder))
            {
                return filePath;
            }

            var folder = rootFolder!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return filePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)
                ? filePath.Substring(folder.Length)
                : filePath
                ;
        }
    }
}
