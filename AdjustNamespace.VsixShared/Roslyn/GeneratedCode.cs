using System;

namespace AdjustNamespace.Roslyn
{
    /// <summary>
    /// The C# files which are not written by the user but are produced out of something else:
    /// the code behind of a xaml file (<c>obj\...\App.g.i.cs</c>), the designer files,
    /// everything which lives in the intermediate output folder.
    ///
    /// Such a file is rewritten on the next build out of the source it is generated from,
    /// hence a declaration found in it is not a declaration of its own: the namespace of
    /// the code behind of a xaml file follows the <c>x:Class</c> of that xaml, which the
    /// adjusting changes at the very same moment.
    /// </summary>
    public static class GeneratedCode
    {
        /// <summary>
        /// The name suffixes of the generated files. The suffix of the whole path is compared,
        /// which is the same as the suffix of the file name for all of them.
        /// </summary>
        private static readonly string[] _generatedFileSuffixes =
        {
            ".g.cs",
            ".g.i.cs",
            ".designer.cs",
            ".generated.cs"
        };

        /// <summary>
        /// The file is a generated one and is not a part of the sources of the solution.
        /// </summary>
        /// <param name="filePath">Full path of the file. An unknown path (<c>null</c> or empty) is not a generated one.</param>
        public static bool IsGeneratedFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            foreach (var suffix in _generatedFileSuffixes)
            {
                if (filePath!.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            //the intermediate output folder, whatever the name of the file in it is
            return filePath!.Replace('/', '\\').IndexOf(
                "\\obj\\",
                StringComparison.OrdinalIgnoreCase
                ) >= 0;
        }
    }
}
