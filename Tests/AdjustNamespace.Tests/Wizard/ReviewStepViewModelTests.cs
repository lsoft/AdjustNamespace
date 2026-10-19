using AdjustNamespace.TextReferences;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.Tests.TextReferences;
using AdjustNamespace.UI;
using AdjustNamespace.UI.StepFactory;
using AdjustNamespace.UI.ViewModel;
using AdjustNamespace.UI.ViewModel.Review;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Xunit;

namespace AdjustNamespace.Tests.Wizard
{
    /// <summary>
    /// Tests of what the review step decides: the filter by level, the check boxes the mode
    /// presets and the buttons that check and uncheck them, and what the next step gets.
    /// The controls themselves are looked at in a running Visual Studio only.
    /// </summary>
    public class ReviewStepViewModelTests
    {
        private const string FileOne = @"C:\sln\App\One.cs";
        private const string FileTwo = @"C:\sln\App\Two.cs";

        /// <summary>
        /// A scan with one match of every level, two of them in the first file.
        /// </summary>
        private static TextReferenceScanResult Scan(TextReferenceMode mode)
        {
            var set = TextReferenceCorpus.Set();

            var matches = new List<TextReferenceMatch>();
            matches.AddRange(TextReferenceScanner.ScanCSharpText(
                set,
                FileOne,
                "class C { void M() { var t = Type.GetType(\"Legacy.Models.Customer, Lib\"); const string N = \"Legacy.Models.Order\"; } }",
                false
                ));
            matches.AddRange(TextReferenceScanner.ScanCSharpText(
                set,
                FileTwo,
                "class C {\n    /// <summary>Legacy.Models.Customer</summary>\n    string s = \"see Legacy.Models.Order here\";\n}",
                true
                ));

            return TextReferenceScanner.Finish(matches, new TextReferenceOptions(mode, TextReferenceKinds.All));
        }

        private sealed class FakeHost : IWizardHost
        {
            public bool Closed { get; private set; }

            public System.Threading.Tasks.Task ShowAsync(UserControl view, ChainViewModel viewModel)
            {
                return System.Threading.Tasks.Task.CompletedTask;
            }

            public void Close()
            {
                Closed = true;
            }
        }

        private sealed class Recorder<T> : IStepFactory<T>
        {
            public List<T> Created { get; } = new();

            public System.Threading.Tasks.Task CreateAsync(T parameters)
            {
                Created.Add(parameters);
                return System.Threading.Tasks.Task.CompletedTask;
            }
        }

        private sealed class Wizard
        {
            public FakeHost Host { get; } = new();

            public Recorder<PerformingParameters> Performing { get; } = new();

            public Recorder<SelectedStepParameters> Selection { get; } = new();

            public ReviewStepViewModel ViewModel { get; }

            public TextReferenceScanResult Scan { get; }

            public Wizard(TextReferenceMode mode)
            {
                Scan = ReviewStepViewModelTests.Scan(mode);

                using var solution = new TestSolution();

                ViewModel = new ReviewStepViewModel(
                    solution.Context,
                    Host,
                    () => Selection,
                    Performing,
                    new ReviewParameters(
                        new HashSet<string> { FileOne, FileTwo },
                        new List<string> { FileOne },
                        new NamespaceReplaceRegex("a", "b"),
                        Array.Empty<AdjustNamespace.Adjusting.Plan.AdjustBlock>(),
                        Scan
                        )
                    );
            }
        }

        private static List<ReviewMatchViewModel> Rows(ReviewStepViewModel viewModel)
        {
            return viewModel.Files.SelectMany(f => f.Matches).ToList();
        }

        [Fact]
        public void The_matches_are_grouped_by_file_and_preset_by_the_mode()
        {
            var wizard = new Wizard(TextReferenceMode.Balanced);

            //the unlikely match (a sentence) is hidden by default
            Assert.Equal(2, wizard.ViewModel.Files.Count);
            Assert.Equal(new[] { FileOne, FileTwo }, wizard.ViewModel.Files.Select(f => f.FilePath).ToArray());

            var one = Rows(wizard.ViewModel).Where(r => r.Match.FilePath == FileOne).ToList();
            Assert.Equal(2, one.Count);
            Assert.All(one, r => Assert.True(r.IsChecked));

            //the comment is Possible: offered and unchecked
            var comment = Assert.Single(Rows(wizard.ViewModel), r => r.Match.Place == TextReferencePlace.Comment);
            Assert.Equal(ConfidenceLevel.Possible, comment.Match.Level);
            Assert.False(comment.IsChecked);

            Assert.DoesNotContain(Rows(wizard.ViewModel), r => r.Match.Level == ConfidenceLevel.Unlikely);
        }

        [Fact]
        public void The_check_box_is_the_approval_of_the_match()
        {
            var wizard = new Wizard(TextReferenceMode.Balanced);

            var comment = Rows(wizard.ViewModel).Single(r => r.Match.Place == TextReferencePlace.Comment);

            comment.IsChecked = true;

            Assert.True(comment.Match.IsApproved);
            Assert.Equal(3, wizard.Scan.Approved.Count());
            Assert.Contains("3 checked", wizard.ViewModel.Summary);
        }

        [Fact]
        public void The_filter_by_level_hides_and_shows_the_rows()
        {
            var wizard = new Wizard(TextReferenceMode.Balanced);

            wizard.ViewModel.ShowPossible = false;
            Assert.DoesNotContain(Rows(wizard.ViewModel), r => r.Match.Level == ConfidenceLevel.Possible);
            Assert.Single(wizard.ViewModel.Files);

            wizard.ViewModel.ShowCertain = false;
            wizard.ViewModel.ShowLikely = false;
            Assert.Empty(wizard.ViewModel.Files);

            wizard.ViewModel.ShowUnlikely = true;
            var unlikely = Assert.Single(Rows(wizard.ViewModel));
            Assert.Equal(ConfidenceLevel.Unlikely, unlikely.Match.Level);
            Assert.False(unlikely.IsChecked);
        }

        [Fact]
        public void An_unlikely_match_can_be_checked_by_hand()
        {
            var wizard = new Wizard(TextReferenceMode.Balanced);

            wizard.ViewModel.ShowUnlikely = true;

            var unlikely = Rows(wizard.ViewModel).Single(r => r.Match.Level == ConfidenceLevel.Unlikely);
            unlikely.IsChecked = true;

            Assert.Contains(unlikely.Match, wizard.Scan.Approved);
        }

        [Fact]
        public void The_buttons_check_and_uncheck_the_matches()
        {
            var wizard = new Wizard(TextReferenceMode.ReportOnly);

            //the report only mode checks nothing
            Assert.All(Rows(wizard.ViewModel), r => Assert.False(r.IsChecked));

            wizard.ViewModel.CheckLikelyCommand.Execute(null);
            Assert.All(
                Rows(wizard.ViewModel),
                r => Assert.Equal(r.Match.Level >= ConfidenceLevel.Likely, r.IsChecked)
                );

            wizard.ViewModel.CheckAllCommand.Execute(null);
            Assert.All(Rows(wizard.ViewModel), r => Assert.True(r.IsChecked));

            //the hidden unlikely match is not checked by `check all`
            Assert.DoesNotContain(wizard.Scan.Approved, m => m.Level == ConfidenceLevel.Unlikely);

            wizard.ViewModel.UncheckAllCommand.Execute(null);
            Assert.Empty(wizard.Scan.Approved);
        }

        [Fact]
        public void The_row_shows_the_line_with_the_name_highlighted_and_the_change()
        {
            var wizard = new Wizard(TextReferenceMode.Balanced);

            var row = Rows(wizard.ViewModel).Single(r => r.Match.Place == TextReferencePlace.KnownTypeNameArgument);

            Assert.Equal("Certain", row.LevelText);
            Assert.Equal("Legacy.Models.Customer", row.LineMatch);
            Assert.StartsWith("class C { void M() { var t = Type.GetType(\"", row.LineBefore);
            Assert.StartsWith(", Lib\")", row.LineAfter);
            Assert.Equal("Legacy.Models.Customer -> Lib.Models.Customer", row.Change);
            Assert.Contains("string passed as a type name +95", row.ToolTip);
            Assert.Equal(1, row.Line);
        }

        [Fact]
        public void The_indentation_of_the_line_is_not_shown()
        {
            var wizard = new Wizard(TextReferenceMode.Balanced);

            var row = Rows(wizard.ViewModel).Single(r => r.Match.Place == TextReferencePlace.Comment);

            Assert.StartsWith("/// <summary>", row.LineBefore);
        }

        [Fact]
        public async Task Next_hands_the_files_and_the_reviewed_scan_to_the_run()
        {
            var wizard = new Wizard(TextReferenceMode.Balanced);

            wizard.ViewModel.UncheckAllCommand.Execute(null);

            wizard.ViewModel.NextCommand.Execute(null);
            await System.Threading.Tasks.Task.Delay(50);

            var parameters = Assert.Single(wizard.Performing.Created);

            Assert.Equal(new[] { FileOne }, parameters.SubjectFilePaths.ToArray());
            Assert.Same(wizard.Scan, parameters.TextReferences);
            Assert.Empty(parameters.TextReferences.Approved);
            Assert.Equal("a", parameters.ReplaceRegex.ReplaceRegex);
        }

        [Fact]
        public async Task Previous_goes_back_to_the_selection_with_the_options_of_the_search()
        {
            var wizard = new Wizard(TextReferenceMode.Conservative);

            wizard.ViewModel.PreviousCommand.Execute(null);
            await System.Threading.Tasks.Task.Delay(50);

            var parameters = Assert.Single(wizard.Selection.Created);

            Assert.Equal(new[] { FileOne, FileTwo }.OrderBy(p => p), parameters.FilePaths.OrderBy(p => p));
            Assert.Equal(TextReferenceMode.Conservative, parameters.TextReferenceOptions!.Mode);
        }

        [Fact]
        public void Cancel_closes_the_wizard()
        {
            var wizard = new Wizard(TextReferenceMode.Balanced);

            wizard.ViewModel.CancelCommand.Execute(null);

            Assert.True(wizard.Host.Closed);
        }

        [Fact]
        public async Task A_scan_with_nothing_to_show_skips_the_review()
        {
            var performing = new Recorder<PerformingParameters>();

            var factory = new ReviewStepFactory(
                null!,
                new FakeHost(),
                () => new Recorder<SelectedStepParameters>(),
                performing
                );

            await factory.CreateAsync(
                new ReviewParameters(
                    new HashSet<string> { FileOne },
                    new List<string> { FileOne },
                    new NamespaceReplaceRegex(string.Empty, string.Empty),
                    Array.Empty<AdjustNamespace.Adjusting.Plan.AdjustBlock>(),
                    TextReferenceScanResult.Empty(TextReferenceOptions.Default)
                    )
                );

            var parameters = Assert.Single(performing.Created);

            Assert.Equal(new[] { FileOne }, parameters.SubjectFilePaths.ToArray());
        }
    }
}
