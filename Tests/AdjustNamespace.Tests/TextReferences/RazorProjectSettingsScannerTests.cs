using AdjustNamespace.TextReferences;
using System.Linq;
using Xunit;

namespace AdjustNamespace.Tests.TextReferences
{
    /// <summary>
    /// Tests of the scanners of the Razor files, the project files and the settings files, with the
    /// score they give: the corpus of the files other than C#.
    /// </summary>
    public class RazorProjectSettingsScannerTests
    {
        private static System.Collections.Generic.List<TextReferenceMatch> Scan(
            TextReferenceFileKind kind,
            string path,
            string text
            )
        {
            return TextReferenceScanner.ScanText(
                TextReferenceCorpus.Set(),
                kind,
                path,
                text,
                TextReferenceOptions.Default
                );
        }

        private static TextReferenceMatch Razor(string text, string path = @"C:\sln\Web\Views\Home\Index.cshtml")
        {
            return Assert.Single(Scan(TextReferenceFileKind.Razor, path, text));
        }

        // ---- Razor ----

        [Theory]
        [InlineData("@model Legacy.Models.Customer")]
        [InlineData("@model List<Legacy.Models.Customer>")]
        [InlineData("@model IEnumerable<Legacy.Models.Customer?>")]
        [InlineData("@inherits Legacy.Models.Customer")]
        [InlineData("@inject Legacy.Models.Order order")]
        [InlineData("@inject IRepo<Legacy.Models.Order> orders")]
        [InlineData("@typeparam T where T : Legacy.Models.Customer")]
        [InlineData("@implements Legacy.Models.Customer")]
        [InlineData("   @model Legacy.Models.Customer")]
        [InlineData("@using static Legacy.Models.Customer")]
        [InlineData("@using Alias = Legacy.Models.Customer")]
        public void A_type_in_a_razor_directive_is_certain(string directive)
        {
            var match = Razor(directive);

            Assert.Equal(ConfidenceLevel.Certain, match.Level);
            Assert.Equal(TextReferencePlace.RazorDirective, match.Place);
            Assert.StartsWith("Legacy.Models.", match.MatchedText);
            Assert.Equal(match.MatchedText.Length, match.Span.Length);
        }

        [Theory]
        [InlineData("@using Legacy.Models")]
        [InlineData("@using Legacy.Models;")]
        [InlineData("@using global::Legacy.Models")]
        [InlineData("@using Alias = Legacy.Models")]
        [InlineData("@namespace Legacy.Models")]
        public void An_emptied_namespace_in_a_directive_is_likely_and_never_certain(string directive)
        {
            var match = Razor(directive);

            Assert.Equal(ConfidenceLevel.Likely, match.Level);
            Assert.Equal(89, match.Score.Value);
            Assert.Equal("Legacy.Models", match.MatchedText);
            Assert.Equal("Lib.Models", match.ReplacementText);
            Assert.Null(match.Type);
            Assert.NotNull(match.Namespace);
            Assert.Contains(match.Score.Signals, s => s.Name == "namespace is emptied: never certain");
        }

        [Fact]
        public void A_namespace_that_stays_alive_or_is_split_is_no_match()
        {
            Assert.Empty(Scan(TextReferenceFileKind.Razor, "x.cshtml", "@using Legacy.Alive"));
            Assert.Empty(Scan(TextReferenceFileKind.Razor, "x.cshtml", "@using Legacy.Util"));
            Assert.Empty(Scan(TextReferenceFileKind.Razor, "x.cshtml", "@using Legacy.Other"));
            Assert.Empty(Scan(TextReferenceFileKind.Razor, "x.cshtml", "@using Legacy.Models.Sub"));
        }

        [Fact]
        public void What_is_no_directive_is_no_match()
        {
            var matches = Scan(
                TextReferenceFileKind.Razor,
                "x.cshtml",
                "@* @model Legacy.Models.Customer *@\r\n"
                + "<p>@Model.Name Legacy.Models.Customer</p>\r\n"
                + "<div class=\"@(Legacy.Models.Customer.Css)\"></div>\r\n"
                + "@{ var x = \"@model Legacy.Models.Customer\"; }\r\n"
                );

            Assert.Empty(matches);
        }

        [Fact]
        public void The_directives_of_a_view_imports_file_have_exact_spans_and_lines()
        {
            var text =
                "@using System\r\n"
                + "@using Legacy.Models\r\n"
                + "@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers\r\n"
                + "@model Legacy.Models.Customer\r\n"
                + "@inject Legacy.Models.Order Orders\r\n";

            var matches = Scan(TextReferenceFileKind.Razor, @"C:\sln\Web\Views\_ViewImports.cshtml", text);

            Assert.Equal(
                new[] { "Legacy.Models", "Legacy.Models.Customer", "Legacy.Models.Order" },
                matches.Select(m => m.MatchedText).ToArray()
                );
            Assert.Equal(new[] { 2, 4, 5 }, matches.Select(m => m.Line).ToArray());
            Assert.All(matches, m => Assert.Equal(m.MatchedText, text.Substring(m.Span.Start, m.Span.Length)));
            Assert.Equal("@model Legacy.Models.Customer", matches[1].LineText);
        }

        [Fact]
        public void A_razor_file_in_a_folder_of_test_data_is_worth_less()
        {
            var match = Razor("@model Legacy.Models.Customer", @"C:\sln\Web.Tests\TestData\Index.cshtml");

            Assert.Equal(90, match.Score.Value);
            Assert.Equal(ConfidenceLevel.Certain, match.Level);
        }

        // ---- the project file ----

        [Fact]
        public void The_startup_object_of_a_project_is_certain()
        {
            var project =
@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <StartupObject>Legacy.Models.Customer</StartupObject>
    <RootNamespace>Legacy.Models</RootNamespace>
  </PropertyGroup>
</Project>";

            var match = Assert.Single(Scan(TextReferenceFileKind.Project, @"C:\sln\App\App.csproj", project));

            Assert.Equal(TextReferencePlace.ProjectStartupObject, match.Place);
            Assert.Equal(ConfidenceLevel.Certain, match.Level);
            Assert.Equal(4, match.Line);
            Assert.Equal("Legacy.Models.Customer", project.Substring(match.Span.Start, match.Span.Length));
        }

        [Fact]
        public void The_custom_tool_namespace_of_an_item_is_the_name_of_a_namespace()
        {
            var project =
@"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <Compile Update=""Resources.Designer.cs"">
      <CustomToolNamespace>Legacy.Models</CustomToolNamespace>
    </Compile>
    <EmbeddedResource Update=""Strings.resx"" CustomToolNamespace=""Legacy.Models"" />
    <Compile Include=""Legacy.Models.Customer.cs"" />
  </ItemGroup>
</Project>";

            var matches = Scan(TextReferenceFileKind.Project, @"C:\sln\App\App.csproj", project);

            Assert.Equal(2, matches.Count);
            Assert.All(matches, m => Assert.Equal(TextReferencePlace.CustomToolNamespace, m.Place));
            Assert.All(matches, m => Assert.Equal(ConfidenceLevel.Likely, m.Level));
            Assert.All(matches, m => Assert.Equal("Lib.Models", m.ReplacementText));
        }

        // ---- the settings file ----

        [Fact]
        public void A_settings_file_names_a_namespace_and_the_types_of_its_settings()
        {
            var settings =
@"<?xml version='1.0' encoding='utf-8'?>
<SettingsFile xmlns=""http://schemas.microsoft.com/VisualStudio/2004/01/settings"" CurrentProfile=""(Default)"" GeneratedClassNamespace=""Legacy.Models"" GeneratedClassName=""Settings"">
  <Profiles />
  <Settings>
    <Setting Name=""Owner"" Type=""Legacy.Models.Customer, Lib"" Scope=""User"">
      <Value Profile=""(Default)"" />
    </Setting>
    <Setting Name=""Text"" Type=""System.String"" Scope=""User"">
      <Value Profile=""(Default)"">Legacy.Models.Customer</Value>
    </Setting>
  </Settings>
</SettingsFile>";

            var matches = Scan(TextReferenceFileKind.Settings, @"C:\sln\App\Properties\Settings.settings", settings);

            Assert.Equal(2, matches.Count);

            var @namespace = matches.Single(m => m.Namespace != null);
            Assert.Equal(TextReferencePlace.SettingsNamespace, @namespace.Place);
            Assert.Equal(ConfidenceLevel.Likely, @namespace.Level);

            var type = matches.Single(m => m.Type != null);
            Assert.Equal(TextReferencePlace.XmlTypeAttribute, type.Place);
            Assert.Equal(ConfidenceLevel.Certain, type.Level);
            Assert.Equal("Legacy.Models.Customer", settings.Substring(type.Span.Start, type.Span.Length));
        }

        // ---- which files are searched ----

        [Theory]
        [InlineData(@"C:\sln\Web\Views\Index.cshtml", TextReferenceFileKind.Razor)]
        [InlineData(@"C:\sln\Web\Pages\Counter.razor", TextReferenceFileKind.Razor)]
        [InlineData(@"C:\sln\Web\_ViewImports.CSHTML", TextReferenceFileKind.Razor)]
        [InlineData(@"C:\sln\App\App.csproj", TextReferenceFileKind.Project)]
        [InlineData(@"C:\sln\App\Properties\Settings.settings", TextReferenceFileKind.Settings)]
        public void The_extension_tells_the_kind_of_a_file(string path, TextReferenceFileKind expected)
        {
            Assert.Equal(expected, TextReferenceFiles.Classify(path, TextReferenceOptions.Default, @"C:\sln"));
        }

        [Theory]
        [InlineData(@"C:\sln\App\Class1.cs")]
        [InlineData(@"C:\sln\App\readme.md")]
        [InlineData(@"C:\sln\App\App.vbproj")]
        [InlineData(@"C:\sln\App\bin\Debug\Index.cshtml")]
        [InlineData(@"C:\sln\App\obj\Debug\App.csproj")]
        [InlineData(@"C:\sln\App\node_modules\x\y.razor")]
        [InlineData(@"C:\sln\.git\x.settings")]
        [InlineData(@"C:\sln\.vs\x.csproj")]
        public void Other_files_and_build_outputs_are_not_searched(string path)
        {
            Assert.Null(TextReferenceFiles.Classify(path, TextReferenceOptions.Default, @"C:\sln"));
        }

        [Fact]
        public void A_folder_of_the_solution_called_bin_is_looked_for_below_the_solution_folder_only()
        {
            //the solution itself lies under a folder named `obj`: that is not a build output
            Assert.Equal(
                TextReferenceFileKind.Razor,
                TextReferenceFiles.Classify(@"C:\obj\sln\Web\Index.cshtml", TextReferenceOptions.Default, @"C:\obj\sln")
                );
        }

        [Fact]
        public void A_switched_off_kind_is_not_searched()
        {
            var razorOff = TextReferenceOptions.Default.WithKinds(TextReferenceKinds.Default & ~TextReferenceKinds.Razor);
            var projectOff = TextReferenceOptions.Default.WithKinds(TextReferenceKinds.Default & ~TextReferenceKinds.ProjectFiles);

            Assert.Null(TextReferenceFiles.Classify(@"C:\sln\Web\Index.cshtml", razorOff, @"C:\sln"));
            Assert.NotNull(TextReferenceFiles.Classify(@"C:\sln\App\App.csproj", razorOff, @"C:\sln"));

            Assert.Null(TextReferenceFiles.Classify(@"C:\sln\App\App.csproj", projectOff, @"C:\sln"));
            Assert.Null(TextReferenceFiles.Classify(@"C:\sln\App\Settings.settings", projectOff, @"C:\sln"));
        }

        [Theory]
        [InlineData("Services.xml", @"C:\sln\App\Services.xml", true)]
        [InlineData("services.XML", @"C:\sln\App\Services.xml", true)]
        [InlineData("*.xml", @"C:\sln\App\Services.xml", true)]
        [InlineData("Services.xml", @"C:\sln\App\Other.xml", false)]
        [InlineData(@"App\Config\*.json", @"C:\sln\App\Config\a.json", true)]
        [InlineData("App/Config/*.json", @"C:\sln\App\Config\a.json", true)]
        [InlineData(@"App\Config\*.json", @"C:\sln\App\Other\a.json", false)]
        [InlineData(@"Config\?.json", @"C:\sln\Config\a.json", true)]
        [InlineData(@"Config\?.json", @"C:\sln\Config\ab.json", false)]
        public void The_patterns_of_the_settings_match_a_name_or_a_path_relative_to_the_solution(string pattern, string path, bool expected)
        {
            Assert.Equal(expected, TextReferenceFiles.MatchesAny(path, new[] { pattern }, @"C:\sln"));
        }
    }
}
