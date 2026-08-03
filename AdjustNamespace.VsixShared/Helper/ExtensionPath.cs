using System.IO;
using System.Reflection;

namespace AdjustNamespace.Helper
{
    /// <summary>
    /// Helpers which resolve the paths relative to the installed extension folder.
    /// </summary>
    public static class ExtensionPath
    {
        /// <summary>
        /// Full path to the given subfolder of the extension folder.
        /// </summary>
        /// <exception cref="InvalidOperationException">The given path is rooted.</exception>
        public static string GetWorkingDirectory(
            this string folderPath
            )
        {
            if (folderPath is null)
            {
                throw new ArgumentNullException(nameof(folderPath));
            }

            if (Path.IsPathRooted(folderPath))
            {
                throw new InvalidOperationException("Relative path should not be rooted!");
            }


            var fi = new FileInfo(Assembly.GetExecutingAssembly().Location);
            var di = fi.Directory.FullName;

            var result = Path.Combine(
                di,
                folderPath
                );

            return result;
        }

        /// <summary>
        /// Full path to the given file of the extension folder
        /// (an already rooted path is returned as is).
        /// </summary>
        public static string GetFullPathToFile(
            this string fileName
            )
        {
            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            if (Path.IsPathRooted(fileName))
            {
                return
                    fileName;
            }

            var fi = new FileInfo(Assembly.GetExecutingAssembly().Location);
            var di = fi.Directory.FullName;

            var result = Path.Combine(
                di,
                fileName
                );

            return result;
        }
    }
}
