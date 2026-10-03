using System;
using System.IO;

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
            ".generated.cs",
            //the parts of the class of a xaml file the .NET MAUI source generator writes:
            //the code behind (fields, InitializeComponent) and, with the SourceGen inflator,
            //the inflated body of InitializeComponent
            ".sg.cs",
            ".xsg.cs"
        };

        /// <summary>
        /// The file is generated again by every build out of something else, so its namespace
        /// follows that source by itself: everything <see cref="IsGeneratedFile"/> knows except
        /// a <c>.designer.cs</c>. A designer file (of a Windows Forms form, for example) is
        /// written by a designer once and is a part of the sources from then on: the build does
        /// not touch it, and its namespace has to be moved together with the other part of its
        /// partial class.
        /// </summary>
        public static bool IsWrittenByTheBuild(string? filePath)
        {
            if (!IsGeneratedFile(filePath))
            {
                return false;
            }

            if (IsInIntermediateFolder(filePath!))
            {
                return true;
            }

            return !filePath!.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The file is written by the build out of the xaml file whose code behind is the given
        /// one: <c>obj\...\App.g.i.cs</c> or <c>obj\...\App.g.cs</c> of WPF and WinUI for
        /// <c>App.xaml.cs</c>, <c>obj\...\Views_MainPage.xaml.sg.cs</c> of .NET MAUI for
        /// <c>Views\MainPage.xaml.cs</c>. Everything such a file declares lives in the namespace
        /// of the <c>x:Class</c> of that xaml.
        /// </summary>
        /// <param name="filePath">The file which may be generated.</param>
        /// <param name="codeBehindFilePath">The code behind of a xaml file (<c>App.xaml.cs</c>,
        /// <c>MainView.axaml.cs</c>); any other file has no generated files of this kind.</param>
        public static bool IsGeneratedOutOfXamlOf(string? filePath, string codeBehindFilePath)
        {
            if (codeBehindFilePath is null)
            {
                throw new ArgumentNullException(nameof(codeBehindFilePath));
            }

            if (!IsWrittenByTheBuild(filePath))
            {
                return false;
            }

            var codeBehindName = Path.GetFileName(codeBehindFilePath);

            var xamlIndex = codeBehindName.IndexOf(".xaml.cs", StringComparison.OrdinalIgnoreCase);
            if (xamlIndex < 0)
            {
                xamlIndex = codeBehindName.IndexOf(".axaml.cs", StringComparison.OrdinalIgnoreCase);
            }

            if (xamlIndex <= 0)
            {
                return false;
            }

            var stem = codeBehindName.Substring(0, xamlIndex);
            var name = Path.GetFileName(filePath!);

            return name.StartsWith(stem + ".", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_" + stem + ".xaml.sg.cs", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_" + stem + ".xaml.xsg.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInIntermediateFolder(string filePath)
        {
            return filePath.Replace('/', '\\').IndexOf(
                "\\obj\\",
                StringComparison.OrdinalIgnoreCase
                ) >= 0;
        }

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
