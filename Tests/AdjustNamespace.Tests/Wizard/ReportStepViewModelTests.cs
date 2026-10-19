using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Adjusting.Report;
using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.UI;
using AdjustNamespace.UI.ViewModel;
using AdjustNamespace.UI.ViewModel.Reporting;
using System.Linq;
using System.Windows.Controls;
using Xunit;

namespace AdjustNamespace.Tests.Wizard
{
    /// <summary>
    /// Tests of the report step: everything it says is decided by the core, so what is left to
    /// check here is that the viewmodel passes it on and that Close closes the wizard.
    /// </summary>
    public class ReportStepViewModelTests
    {
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

        private static AdjustReport Report(AdjustSessionOutcome outcome, string? failure = null)
        {
            var builder = new AdjustReportBuilder(@"C:\sln");

            builder.RecordFile(
                AdjustFileOutcome.Adjusted(@"C:\sln\App\A.cs", new[] { new NamespaceChange("Old", "App") })
                );
            builder.RecordFile(
                AdjustFileOutcome.Blocked(AdjustBlock.Create(@"C:\sln\App\B.cs", AdjustBlockKind.NoProject))
                );
            builder.RecordEdit(@"C:\sln\App\A.cs", @"C:\sln\App\C.cs", AdjustNamespace.Adjusting.Edit.EditKind.UsingAdded, 1);

            return builder.Build(outcome, failure);
        }

        [Fact]
        public void The_viewmodel_shows_the_summary_and_the_sections_of_the_core()
        {
            var viewModel = new ReportStepViewModel(
                new FakeHost(),
                new ReportParameters(Report(AdjustSessionOutcome.Completed), false)
                );

            Assert.Equal("Adjusted 1 file(s), blocked 1, skipped 0, not processed 0.", viewModel.Summary);
            Assert.False(viewModel.HasNote);

            Assert.Equal(new[] { "Adjusted (1)", "Blocked (1)" }, viewModel.Sections.Select(s => s.Title).ToArray());

            var adjusted = viewModel.Sections[0].Children.Single();

            Assert.Equal(@"App\A.cs", adjusted.Title);
            Assert.Equal(@"C:\sln\App\A.cs", adjusted.FilePath);
            Assert.Equal(@"App\C.cs", adjusted.Children.Single().Title);
            Assert.True(viewModel.Sections[0].IsExpanded);
        }

        [Fact]
        public void A_cancelled_run_and_a_failed_one_have_a_note()
        {
            var cancelled = new ReportStepViewModel(
                new FakeHost(),
                new ReportParameters(Report(AdjustSessionOutcome.Cancelled), false)
                );

            Assert.True(cancelled.HasNote);
            Assert.Contains("cancelled", cancelled.Note);

            var failed = new ReportStepViewModel(
                new FakeHost(),
                new ReportParameters(Report(AdjustSessionOutcome.Failed, "boom"), true)
                );

            Assert.Contains("boom", failed.Note);
            Assert.Contains("rolled back", failed.Note);

            var failedKept = new ReportStepViewModel(
                new FakeHost(),
                new ReportParameters(Report(AdjustSessionOutcome.Failed, "boom"), false)
                );

            Assert.DoesNotContain("rolled back", failedKept.Note);
        }

        [Fact]
        public void Close_closes_the_wizard()
        {
            var host = new FakeHost();
            var viewModel = new ReportStepViewModel(host, new ReportParameters(Report(AdjustSessionOutcome.Completed), false));

            viewModel.CloseCommand.Execute(null);

            Assert.True(host.Closed);
        }

        [Fact]
        public void The_open_log_button_is_enabled_when_there_is_a_log()
        {
            var viewModel = new ReportStepViewModel(
                new FakeHost(),
                new ReportParameters(Report(AdjustSessionOutcome.Completed), false)
                );

            var log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AdjustNamespace.vs.log");

            Assert.Equal(System.IO.File.Exists(log), viewModel.OpenLogCommand.CanExecute(null));
        }
    }
}
