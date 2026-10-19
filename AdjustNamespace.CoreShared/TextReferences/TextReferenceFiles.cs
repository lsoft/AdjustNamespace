using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Which files the search looks into besides the C# documents, and of what kind they are.
    ///
    /// The extension of a file says it (<c>.cshtml</c>, <c>.csproj</c>, <c>.settings</c>, ...),
    /// the switches of the options decide whether that kind is searched, and the settings of the
    /// solution may name other xml and json files. Everything under <c>bin</c>, <c>obj</c>,
    /// <c>node_modules</c> and the folders of the tools is a build output or a copy and is
    /// never searched.
    /// </summary>
    public static class TextReferenceFiles
    {
        private static readonly string[] ExcludedFolders = { "bin", "obj", "node_modules", ".git", ".vs" };

        /// <summary>
        /// The extensions of the files the search can look into. A host that has to walk the
        /// folders itself (the console utility) reports the files of these extensions only.
        /// </summary>
        public static IReadOnlyList<string> CandidateExtensions { get; } = new[]
        {
            ".cshtml",
            ".razor",
            ".csproj",
            ".settings",
            ".config",
            ".json",
            ".xml"
        };

        /// <summary>
        /// The folder is a build output or belongs to a tool, and nothing in it is searched.
        /// </summary>
        public static bool IsExcludedFolderName(string folderName)
        {
            return ExcludedFolders.Contains(folderName, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The kind of the file, or <c>null</c> if the search does not look into it: it is
        /// of no kind the search knows, the kind is switched off, or it lies in a build output.
        /// </summary>
        /// <param name="filePath">Full path to the file.</param>
        /// <param name="options">The options of the search.</param>
        /// <param name="solutionFolder">
        /// Folder of the solution: the excluded folders are looked for under it only, and the
        /// patterns of the settings are relative to it.
        /// </param>
        public static TextReferenceFileKind? Classify(
            string filePath,
            TextReferenceOptions options,
            string? solutionFolder
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (IsExcluded(filePath, solutionFolder))
            {
                return null;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            TextReferenceFileKind? kind;
            TextReferenceKinds needed;

            switch (extension)
            {
                case ".cshtml":
                case ".razor":
                    kind = TextReferenceFileKind.Razor;
                    needed = TextReferenceKinds.Razor;
                    break;

                case ".csproj":
                    kind = TextReferenceFileKind.Project;
                    needed = TextReferenceKinds.ProjectFiles;
                    break;

                case ".settings":
                    kind = TextReferenceFileKind.Settings;
                    needed = TextReferenceKinds.ProjectFiles;
                    break;

                case ".config":
                    kind = TextReferenceFileKind.XmlConfig;
                    needed = TextReferenceKinds.XmlConfigs;
                    break;

                case ".json":
                    if (!IsAppSettings(filePath) && !IsNamedBySettings(filePath, options, solutionFolder))
                    {
                        return null;
                    }

                    kind = TextReferenceFileKind.Json;
                    needed = TextReferenceKinds.Json;
                    break;

                default:
                    //any other file is searched only if the settings of the solution name it:
                    //an xml file is an xml config then, a json file is handled above
                    if (extension != ".xml" || !IsNamedBySettings(filePath, options, solutionFolder))
                    {
                        return null;
                    }

                    kind = TextReferenceFileKind.XmlConfig;
                    needed = TextReferenceKinds.XmlConfigs;
                    break;
            }

            return (options.Kinds & needed) != 0 ? kind : null;
        }

        /// <summary>
        /// <c>appsettings.json</c>, <c>appsettings.Development.json</c>, ...
        /// </summary>
        private static bool IsAppSettings(string filePath)
        {
            return Path.GetFileName(filePath).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNamedBySettings(string filePath, TextReferenceOptions options, string? solutionFolder)
        {
            return options.ExtraFiles.Count > 0 && MatchesAny(filePath, options.ExtraFiles, solutionFolder);
        }

        /// <summary>
        /// The file lies in a folder the search never looks into.
        /// </summary>
        public static bool IsExcluded(string filePath, string? solutionFolder)
        {
            var path = ReportPathsRelative(filePath, solutionFolder);

            var folders = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            //the last part is the name of the file
            for (var i = 0; i < folders.Length - 1; i++)
            {
                if (IsExcludedFolderName(folders[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The file matches one of the patterns of the settings: a name (<c>Services.xml</c>), a
        /// path relative to the solution folder (<c>Config\*.json</c>) or any of them with
        /// <c>*</c> and <c>?</c>.
        /// </summary>
        public static bool MatchesAny(
            string filePath,
            System.Collections.Generic.IEnumerable<string> patterns,
            string? solutionFolder
            )
        {
            var relative = ReportPathsRelative(filePath, solutionFolder).Replace('/', '\\');
            var name = Path.GetFileName(filePath);

            foreach (var pattern in patterns)
            {
                var normalized = pattern.Trim().Replace('/', '\\');
                if (normalized.Length == 0)
                {
                    continue;
                }

                var subject = normalized.IndexOf('\\') >= 0 ? relative : name;

                if (WildcardToRegex(normalized).IsMatch(subject))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReportPathsRelative(string filePath, string? solutionFolder)
        {
            return Adjusting.Report.ReportPaths.Relative(filePath, solutionFolder);
        }

        private static Regex WildcardToRegex(string pattern)
        {
            var text = new StringBuilder("^");

            foreach (var c in pattern)
            {
                switch (c)
                {
                    case '*':
                        text.Append(".*");
                        break;

                    case '?':
                        text.Append('.');
                        break;

                    default:
                        text.Append(Regex.Escape(c.ToString()));
                        break;
                }
            }

            text.Append('$');

            return new Regex(text.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
