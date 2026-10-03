using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace AdjustNamespace.Tests.Build
{
    /// <summary>
    /// The automated tests compile the shared code with their own project file and their own
    /// Roslyn, so everything which differs between that and the published extension is out of
    /// their sight. These tests read the project files themselves and check that nothing the
    /// tests rely on differs from what is shipped.
    ///
    /// Both of the following went unnoticed for a long time:
    /// <list type="bullet">
    /// <item>the support of the file scoped namespaces was compiled under <c>#if VS2022</c>,
    /// which the test project and the Debug build of the extension defined, but the Release
    /// build of the extension did not: the published extension did not adjust such files;</item>
    /// <item>the manifest declared Visual Studio 17.0 as the lowest one, while the extension
    /// was compiled against the Roslyn of 17.4 and could not be loaded by an older one.</item>
    /// </list>
    /// </summary>
    public class ProjectConsistencyTests
    {
        private const string VsixProject = @"AdjustNamespace.2022\AdjustNamespace.2022.csproj";
        private const string VsixManifest = @"AdjustNamespace.2022\source.extension.vsixmanifest";

        private static readonly string[] SharedProjectFolders =
        {
            "AdjustNamespace.CoreShared",
            "AdjustNamespace.VsixShared"
        };

        /// <summary>
        /// The Roslyn every Visual Studio release ships with (major.minor). Visual Studio binds
        /// a reference to an older Roslyn to its own one, but never to a newer one, so this is
        /// the newest Roslyn the extension may be compiled against to load into that release.
        /// Extend it when the lowest supported Visual Studio changes.
        /// </summary>
        private static readonly Dictionary<Version, Version> RoslynOfVisualStudio = new Dictionary<Version, Version>
        {
            [new Version(17, 0)] = new Version(4, 0),
            [new Version(17, 1)] = new Version(4, 1),
            [new Version(17, 2)] = new Version(4, 2),
            [new Version(17, 3)] = new Version(4, 3),
            [new Version(17, 4)] = new Version(4, 4),
            [new Version(17, 5)] = new Version(4, 5),
            [new Version(17, 6)] = new Version(4, 6),
            [new Version(17, 7)] = new Version(4, 7),
            [new Version(17, 8)] = new Version(4, 8),
            [new Version(17, 9)] = new Version(4, 9),
            [new Version(17, 10)] = new Version(4, 10),
            [new Version(17, 11)] = new Version(4, 11),
            [new Version(17, 12)] = new Version(4, 12),
            [new Version(17, 13)] = new Version(4, 13),
            [new Version(17, 14)] = new Version(4, 14),
            [new Version(18, 0)] = new Version(5, 0),
        };

        /// <summary>
        /// Every conditional compilation symbol of the shared code has to mean the same in the
        /// published extension and in the tests, otherwise the tests check a code which is
        /// never shipped. <c>DEBUG</c> is the only symbol allowed to differ.
        /// </summary>
        [Fact]
        public void The_shared_code_uses_no_symbol_the_release_extension_lacks()
        {
            var releaseSymbols = DefinedSymbolsOf(VsixProject, "Release");

            var unknownSymbols = (
                from folder in SharedProjectFolders
                from filePath in Directory.EnumerateFiles(PathOf(folder), "*.cs", SearchOption.AllDirectories)
                where !IsBuildOutput(filePath)
                from symbol in ConditionalSymbolsOf(File.ReadAllText(filePath))
                where symbol != "DEBUG"
                where !releaseSymbols.Contains(symbol)
                select $"{symbol} in {filePath}"
                )
                .Distinct()
                .ToList();

            Assert.Empty(unknownSymbols);
        }

        /// <summary>
        /// The Debug and the Release builds of the extension differ in <c>DEBUG</c> only:
        /// a symbol defined in one of them only means that one of them ships another code.
        /// </summary>
        [Fact]
        public void The_debug_and_the_release_extension_define_the_same_symbols()
        {
            var debugSymbols = DefinedSymbolsOf(VsixProject, "Debug");
            var releaseSymbols = DefinedSymbolsOf(VsixProject, "Release");

            debugSymbols.Remove("DEBUG");

            Assert.Equal(
                releaseSymbols.OrderBy(s => s),
                debugSymbols.OrderBy(s => s)
                );
        }

        /// <summary>
        /// The lowest Visual Studio of the manifest has to be able to load the extension:
        /// no Roslyn reference of the extension may be newer than the Roslyn of that release.
        /// </summary>
        [Fact]
        public void The_lowest_visual_studio_of_the_manifest_loads_the_roslyn_of_the_extension()
        {
            var lowestVisualStudio = LowestVisualStudioOfManifest();

            Assert.True(
                RoslynOfVisualStudio.TryGetValue(lowestVisualStudio, out var roslynOfLowest),
                $"The Roslyn of Visual Studio {lowestVisualStudio} is unknown, add it to {nameof(RoslynOfVisualStudio)}"
                );

            var roslynReferences = PackageReferencesOf(VsixProject)
                .Where(r => r.Key.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)
                    || r.Key == "Microsoft.VisualStudio.LanguageServices")
                .ToList();

            Assert.NotEmpty(roslynReferences);

            foreach (var reference in roslynReferences)
            {
                var version = Version.Parse(reference.Value);
                var majorMinor = new Version(version.Major, version.Minor);

                Assert.True(
                    majorMinor <= roslynOfLowest,
                    $"{reference.Key} {reference.Value} is newer than Roslyn {roslynOfLowest} of Visual Studio {lowestVisualStudio}, the lowest one of the manifest"
                    );
            }
        }

        /// <summary>
        /// The prerequisite of the manifest starts at the same Visual Studio as its targets.
        /// </summary>
        [Fact]
        public void The_prerequisite_of_the_manifest_matches_its_targets()
        {
            var manifest = XDocument.Load(PathOf(VsixManifest));

            var prerequisite = manifest
                .Descendants()
                .Single(e => e.Name.LocalName == "Prerequisite")
                .Attribute("Version")!
                .Value;

            Assert.Equal(LowestVisualStudioOfManifest(), LowerBoundOf(prerequisite));
        }

        private static Version LowestVisualStudioOfManifest()
        {
            var manifest = XDocument.Load(PathOf(VsixManifest));

            var lowerBounds = manifest
                .Descendants()
                .Where(e => e.Name.LocalName == "InstallationTarget")
                .Select(e => LowerBoundOf(e.Attribute("Version")!.Value))
                .Distinct()
                .ToList();

            return Assert.Single(lowerBounds);
        }

        /// <summary>
        /// The lower bound of a version range of a manifest: <c>[18.0, 19.0)</c> gives 18.0.
        /// </summary>
        private static Version LowerBoundOf(string range)
        {
            var lower = range.Trim().TrimStart('[', '(').Split(',')[0].Trim();

            return Version.Parse(lower);
        }

        private static HashSet<string> DefinedSymbolsOf(string relativeProjectPath, string configuration)
        {
            var project = XDocument.Load(PathOf(relativeProjectPath));

            var defineConstants = project
                .Descendants()
                .Where(e => e.Name.LocalName == "PropertyGroup")
                .Where(g => (g.Attribute("Condition")?.Value ?? string.Empty).Contains("'" + configuration + "|"))
                .Elements()
                .Where(e => e.Name.LocalName == "DefineConstants")
                .Select(e => e.Value)
                .ToList();

            var value = Assert.Single(defineConstants);

            return new HashSet<string>(
                value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0 && !s.StartsWith("$(", StringComparison.Ordinal))
                );
        }

        private static Dictionary<string, string> PackageReferencesOf(string relativeProjectPath)
        {
            var project = XDocument.Load(PathOf(relativeProjectPath));

            return project
                .Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .ToDictionary(
                    e => e.Attribute("Include")!.Value,
                    e => e.Attribute("Version")?.Value
                        ?? e.Elements().Single(v => v.Name.LocalName == "Version").Value
                    );
        }

        /// <summary>
        /// The symbols of the <c>#if</c> and <c>#elif</c> directives of a source file.
        /// </summary>
        private static IEnumerable<string> ConditionalSymbolsOf(string text)
        {
            foreach (Match directive in Regex.Matches(text, @"^\s*#\s*(?:if|elif)\b(.*)$", RegexOptions.Multiline))
            {
                foreach (Match symbol in Regex.Matches(directive.Groups[1].Value, @"[A-Za-z_][A-Za-z0-9_]*"))
                {
                    if (symbol.Value == "true" || symbol.Value == "false")
                    {
                        continue;
                    }

                    yield return symbol.Value;
                }
            }
        }

        private static bool IsBuildOutput(string filePath)
        {
            var normalized = filePath.Replace('/', '\\');

            return normalized.IndexOf(@"\obj\", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf(@"\bin\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string PathOf(string relativePath)
        {
            return Path.Combine(RepositoryRoot, relativePath);
        }

        /// <summary>
        /// The folder of <c>AdjustNamespace.sln</c>, found upwards from the test assembly.
        /// </summary>
        private static string RepositoryRoot
        {
            get
            {
                var folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (folder != null)
                {
                    if (File.Exists(Path.Combine(folder.FullName, "AdjustNamespace.sln")))
                    {
                        return folder.FullName;
                    }

                    folder = folder.Parent;
                }

                throw new InvalidOperationException("AdjustNamespace.sln is not found above the test assembly");
            }
        }
    }
}
