using AdjustNamespace.Settings;
using AdjustNamespace.TextReferences;
using System.IO;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// Tests of <see cref="TextReferenceOptions"/>: the aggressiveness (which levels are
    /// changed, which are offered), the kinds, and the per solution override that has to keep
    /// the settings files written before 0.8.0 working.
    /// </summary>
    public class TextReferenceOptionsTests
    {
        private static string Matrix(TextReferenceMode mode)
        {
            var options = new TextReferenceOptions(mode, TextReferenceKinds.Default);

            string Describe(ConfidenceLevel level)
            {
                if (options.IsApplied(level))
                {
                    return "apply";
                }

                return options.IsShown(level) ? "offer" : "hide";
            }

            return string.Join(
                " ",
                new[] { ConfidenceLevel.Certain, ConfidenceLevel.Likely, ConfidenceLevel.Possible, ConfidenceLevel.Unlikely }
                    .Select(Describe)
                );
        }

        [Theory]
        [InlineData(TextReferenceMode.Off, "hide hide hide hide")]
        [InlineData(TextReferenceMode.ReportOnly, "offer offer offer hide")]
        [InlineData(TextReferenceMode.Conservative, "apply offer offer hide")]
        [InlineData(TextReferenceMode.Balanced, "apply apply offer hide")]
        [InlineData(TextReferenceMode.Aggressive, "apply apply apply hide")]
        public void The_mode_decides_what_is_changed_and_what_is_offered(TextReferenceMode mode, string expected)
        {
            //Certain Likely Possible Unlikely
            Assert.Equal(expected, Matrix(mode));
        }

        [Fact]
        public void The_default_is_balanced_without_the_comments()
        {
            var options = TextReferenceOptions.Default;

            Assert.Equal(TextReferenceMode.Balanced, options.Mode);
            Assert.False((options.Kinds & TextReferenceKinds.Comments) != 0);
            Assert.True((options.Kinds & TextReferenceKinds.XmlConfigs) != 0);
            Assert.True((options.Kinds & TextReferenceKinds.Json) != 0);
            Assert.True((options.Kinds & TextReferenceKinds.Razor) != 0);
            Assert.True((options.Kinds & TextReferenceKinds.ProjectFiles) != 0);
            Assert.True(options.IsScanEnabled);
            Assert.False(TextReferenceOptions.Off.IsScanEnabled);
        }

        [Theory]
        [InlineData("off", TextReferenceMode.Off)]
        [InlineData("Report", TextReferenceMode.ReportOnly)]
        [InlineData("report-only", TextReferenceMode.ReportOnly)]
        [InlineData("ReportOnly", TextReferenceMode.ReportOnly)]
        [InlineData("CONSERVATIVE", TextReferenceMode.Conservative)]
        [InlineData(" balanced ", TextReferenceMode.Balanced)]
        [InlineData("aggressive", TextReferenceMode.Aggressive)]
        public void A_mode_is_read_in_any_case(string text, TextReferenceMode expected)
        {
            Assert.True(TextReferenceOptions.TryParseMode(text, out var mode));
            Assert.Equal(expected, mode);
        }

        [Fact]
        public void An_unknown_mode_is_refused()
        {
            Assert.False(TextReferenceOptions.TryParseMode("reckless", out _));
        }

        [Fact]
        public void The_kinds_are_read_from_a_list()
        {
            Assert.True(TextReferenceOptions.TryParseKinds("comments, xml;json", out var kinds));
            Assert.Equal(TextReferenceKinds.Comments | TextReferenceKinds.XmlConfigs | TextReferenceKinds.Json, kinds);

            Assert.True(TextReferenceOptions.TryParseKinds("razor,project", out kinds));
            Assert.Equal(TextReferenceKinds.Razor | TextReferenceKinds.ProjectFiles, kinds);

            Assert.True(TextReferenceOptions.TryParseKinds("none", out kinds));
            Assert.Equal(TextReferenceKinds.None, kinds);

            Assert.True(TextReferenceOptions.TryParseKinds("all", out kinds));
            Assert.Equal(TextReferenceKinds.All, kinds);

            Assert.False(TextReferenceOptions.TryParseKinds("xml,yaml", out _));
        }

        [Fact]
        public void A_solution_without_settings_keeps_the_defaults()
        {
            var resolved = TextReferenceOptions.Resolve(TextReferenceOptions.Default, null);

            Assert.Same(TextReferenceOptions.Default, resolved);

            resolved = TextReferenceOptions.Resolve(TextReferenceOptions.Default, new AdjustNamespaceSettings());

            Assert.Equal(TextReferenceOptions.Default.Mode, resolved.Mode);
            Assert.Equal(TextReferenceOptions.Default.Kinds, resolved.Kinds);
        }

        [Fact]
        public void The_settings_of_the_solution_override_the_defaults()
        {
            var settings = new AdjustNamespaceSettings
            {
                TextReferenceMode = "Aggressive",
                ScanComments = true,
                ScanJson = false,
                TextReferenceFiles = { "Services.xml", "Config\\*.json" }
            };

            var resolved = TextReferenceOptions.Resolve(TextReferenceOptions.Default, settings);

            Assert.Equal(TextReferenceMode.Aggressive, resolved.Mode);
            Assert.True((resolved.Kinds & TextReferenceKinds.Comments) != 0);
            Assert.False((resolved.Kinds & TextReferenceKinds.Json) != 0);
            Assert.True((resolved.Kinds & TextReferenceKinds.XmlConfigs) != 0);
            Assert.Equal(new[] { "Services.xml", "Config\\*.json" }, resolved.ExtraFiles.ToArray());
        }

        [Fact]
        public void A_mode_that_cannot_be_read_leaves_the_default()
        {
            var resolved = TextReferenceOptions.Resolve(
                TextReferenceOptions.Default.WithMode(TextReferenceMode.Conservative),
                new AdjustNamespaceSettings { TextReferenceMode = "nonsense" }
                );

            Assert.Equal(TextReferenceMode.Conservative, resolved.Mode);
        }

        /// <summary>
        /// The file written by an older version has no element of the search at all and has to
        /// go on working; so does a file with the elements, through the same serializer.
        /// </summary>
        [Fact]
        public void A_settings_file_of_an_older_version_is_read_and_overrides_nothing()
        {
            using var solution = new Infrastructure.TestSolution();

            File.WriteAllText(
                Path.Combine(solution.SolutionFolder, SettingsReader.SettingFileName),
@"<?xml version=""1.0""?>
<AdjustNamespaceSettings xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <SkippedFolderSuffixes>
    <string>MyProject\A\B</string>
  </SkippedFolderSuffixes>
</AdjustNamespaceSettings>"
                );

            var settings = AdjustContext.ReadSettingsOf(solution.SolutionFolder).Settings;

            Assert.Equal(new[] { @"MyProject\A\B" }, settings.SkippedFolderSuffixes.ToArray());
            Assert.Null(settings.TextReferenceMode);
            Assert.Null(settings.ScanComments);
            Assert.Empty(settings.TextReferenceFiles);

            var resolved = TextReferenceOptions.Resolve(TextReferenceOptions.Default, settings);

            Assert.Equal(TextReferenceOptions.Default.Mode, resolved.Mode);
            Assert.Equal(TextReferenceOptions.Default.Kinds, resolved.Kinds);
        }

        [Fact]
        public void The_new_elements_of_the_settings_file_survive_a_round_trip()
        {
            using var solution = new Infrastructure.TestSolution();

            var original = new AdjustNamespaceSettings
            {
                TextReferenceMode = "ReportOnly",
                ScanComments = true,
                ScanRazor = false,
                TextReferenceFiles = { "Services.xml" }
            };
            original.SkippedFolderSuffixes.Add("A");

            new SettingsReader(solution.SolutionFolder).Save(original);

            var read = AdjustContext.ReadSettingsOf(solution.SolutionFolder).Settings;

            Assert.Equal("ReportOnly", read.TextReferenceMode);
            Assert.True(read.ScanComments);
            Assert.False(read.ScanRazor);
            Assert.Null(read.ScanJson);
            Assert.Equal(new[] { "Services.xml" }, read.TextReferenceFiles.ToArray());
            Assert.Equal(new[] { "A" }, read.SkippedFolderSuffixes.ToArray());
        }
    }
}
