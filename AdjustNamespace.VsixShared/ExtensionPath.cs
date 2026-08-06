using System.IO;
using System.Reflection;

namespace AdjustNamespace
{
    /// <summary>
    /// Resolving the paths relative to the folder the extension is installed into.
    /// </summary>
    public static class ExtensionPath
    {
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
