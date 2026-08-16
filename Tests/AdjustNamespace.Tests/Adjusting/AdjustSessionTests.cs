using AdjustNamespace.Adjusting.Session;
using AdjustNamespace.Namespace;
using AdjustNamespace.Tests.Infrastructure;
using AdjustNamespace.VisualStudio;
using AdjustNamespace.Xaml.BodyProvider;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace AdjustNamespace.Tests.Adjusting
{
    /// <summary>
    /// Tests of <see cref="AdjustSession"/>: one run of the extension over the files the user
    /// has chosen, exactly as the last wizard step starts it.
    ///
    /// This was the body of <c>PerformingViewModel</c> before and could be looked at in
    /// a running Visual Studio only: the order of the stages, the namespace state shared by
    /// all the files of the run and the reaction to a cancel are asserted here now.
    /// The tests of the single steps of a session drive them one by one instead,
    /// see <see cref="AdjustRunner"/>.
    /// </summary>
    public class AdjustSessionTests
    {
        /// <summary>
        /// The usual run: several files are moved into the namespaces of their folders,
        /// the references to them are fixed and the using clause of the namespace both of them
        /// have left is removed by the cleanup at the end of the session.
        /// </summary>
        [Fact]
        public async Task A_session_adjusts_every_chosen_file_and_cleans_up_afterwards()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"First\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", @"Second\Class2.cs",
@"namespace A.B
{
    public class Class2 { }
}
")
                .AddDocument("MyApp", "Consumer.cs",
@"using A.B;

namespace Other
{
    public class Consumer
    {
        public Class1 Create1() => new Class1();

        public Class2 Create2() => new Class2();
    }
}
")
                ;

            var outcome = await RunAsync(
                solution,
                solution.PathOf("MyApp", @"First\Class1.cs"),
                solution.PathOf("MyApp", @"Second\Class2.cs")
                );

            Assert.Equal(AdjustSessionOutcome.Completed, outcome);
            Assert.Contains("namespace MyApp.First", solution.TextOf("MyApp", @"First\Class1.cs"));
            Assert.Contains("namespace MyApp.Second", solution.TextOf("MyApp", @"Second\Class2.cs"));

            var consumer = solution.TextOf("MyApp", "Consumer.cs");

            Assert.DoesNotContain("using A.B;", consumer);
            Assert.Contains("using MyApp.First;", consumer);
            Assert.Contains("using MyApp.Second;", consumer);
            Assert.Empty(await solution.CompilationErrorsAsync());
        }

        /// <summary>
        /// The user may choose a file which is in its target namespace already: there is
        /// nothing to plan for it, and the session goes on with the other files.
        /// </summary>
        [Fact]
        public async Task A_file_which_is_no_subject_to_change_is_skipped()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Settled.cs",
@"namespace MyApp
{
    public class Settled { }
}
")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                ;

            var before = solution.TextOf("MyApp", "Settled.cs");

            var outcome = await RunAsync(
                solution,
                solution.PathOf("MyApp", "Settled.cs"),
                solution.PathOf("MyApp", "Class1.cs")
                );

            Assert.Equal(AdjustSessionOutcome.Completed, outcome);
            Assert.Equal(before, solution.TextOf("MyApp", "Settled.cs"));
            Assert.Contains("namespace MyApp", solution.TextOf("MyApp", "Class1.cs"));
        }

        /// <summary>
        /// The progress line of the wizard is built out of these reports: every chosen file
        /// is reported once and in the given order, and the cleanup of the whole solution
        /// follows all of them.
        /// </summary>
        [Fact]
        public async Task Every_file_is_reported_once_and_the_cleanup_comes_last()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"First\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", @"Second\Class2.cs",
@"namespace A.B
{
    public class Class2 { }
}
")
                ;

            var recorder = new ProgressRecorder();

            await RunAsync(solution, recorder, CancellationToken.None,
                solution.PathOf("MyApp", @"First\Class1.cs"),
                solution.PathOf("MyApp", @"Second\Class2.cs")
                );

            var adjusting = recorder.Reports.FindAll(r => r.Stage == AdjustStage.Adjusting);
            var cleanup = recorder.Reports.FindAll(r => r.Stage == AdjustStage.Cleanup);

            Assert.Equal(
                new[]
                {
                    solution.PathOf("MyApp", @"First\Class1.cs"),
                    solution.PathOf("MyApp", @"Second\Class2.cs")
                },
                adjusting.ConvertAll(r => r.FilePath)
                );
            Assert.Equal(new[] { 1, 2 }, adjusting.ConvertAll(r => r.Current));
            Assert.All(adjusting, r => Assert.Equal(2, r.Total));

            //the cleanup walks the whole solution and not the chosen files only
            Assert.Equal(2, cleanup.Count);

            //all the adjusting is done before the first cleanup report
            Assert.Equal(
                recorder.Reports.Count,
                recorder.Reports.FindIndex(r => r.Stage == AdjustStage.Cleanup) + cleanup.Count
                );
        }

        /// <summary>
        /// A cancelled session is a usual outcome and not an error, and the files behind
        /// the cancelled one are left as they are.
        /// </summary>
        [Fact]
        public async Task A_cancelled_session_leaves_the_files_behind_the_cancel_alone()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"First\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", @"Second\Class2.cs",
@"namespace C.D
{
    public class Class2 { }
}
")
                ;

            var before = solution.TextOf("MyApp", @"Second\Class2.cs");

            using var cts = new CancellationTokenSource();

            //the user presses `Cancel` while the first file is being processed
            var recorder = new ProgressRecorder(r => cts.Cancel());

            var outcome = await RunAsync(solution, recorder, cts.Token,
                solution.PathOf("MyApp", @"First\Class1.cs"),
                solution.PathOf("MyApp", @"Second\Class2.cs")
                );

            Assert.Equal(AdjustSessionOutcome.Cancelled, outcome);
            Assert.Equal(before, solution.TextOf("MyApp", @"Second\Class2.cs"));
        }

        /// <summary>
        /// The changes which have been applied before the cancel are not reverted:
        /// this is what the wizard promises the user.
        /// </summary>
        [Fact]
        public async Task The_changes_of_a_cancelled_session_stay()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", "Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                ;

            using var cts = new CancellationTokenSource();

            //the file has been adjusted already and the cleanup has just started
            var recorder = new ProgressRecorder(
                r =>
                {
                    if (r.Stage == AdjustStage.Cleanup)
                    {
                        cts.Cancel();
                    }
                });

            var outcome = await RunAsync(solution, recorder, cts.Token,
                solution.PathOf("MyApp", "Class1.cs")
                );

            Assert.Equal(AdjustSessionOutcome.Cancelled, outcome);
            Assert.Contains("namespace MyApp", solution.TextOf("MyApp", "Class1.cs"));
        }

        /// <summary>
        /// The solution tree is expensive to walk and is available from the main thread only,
        /// so a whole session asks for it once, no matter how many files it processes.
        /// </summary>
        [Fact]
        public async Task The_solution_tree_is_walked_once_per_session()
        {
            using var solution = new TestSolution()
                .AddProject("MyApp")
                .AddDocument("MyApp", @"First\Class1.cs",
@"namespace A.B
{
    public class Class1 { }
}
")
                .AddDocument("MyApp", @"Second\Class2.cs",
@"namespace A.B
{
    public class Class2 { }
}
")
                ;

            var solutionExplorer = new FakeSolutionExplorer(solution);

            var context = new AdjustContext(
                solution.Workspace,
                solutionExplorer,
                new TargetNamespaceResolver(
                    solution.Settings,
                    new FakeProjectDefaultNamespaceProvider()
                    ),
                new ClosedXamlBodyProviderFactory()
                );

            await new AdjustSession(context, NoRegex())
                .RunAsync(
                    new[]
                    {
                        solution.PathOf("MyApp", @"First\Class1.cs"),
                        solution.PathOf("MyApp", @"Second\Class2.cs")
                    }
                    );

            Assert.Equal(1, solutionExplorer.GetProjectOfEveryFileCallCount);
        }

        private static async System.Threading.Tasks.Task<AdjustSessionOutcome> RunAsync(
            TestSolution solution,
            params string[] subjectFilePaths
            )
        {
            return await RunAsync(solution, null, CancellationToken.None, subjectFilePaths);
        }

        private static async System.Threading.Tasks.Task<AdjustSessionOutcome> RunAsync(
            TestSolution solution,
            IProgress<AdjustProgress>? progress,
            CancellationToken cancellationToken,
            params string[] subjectFilePaths
            )
        {
            return await new AdjustSession(solution.Context, NoRegex())
                .RunAsync(subjectFilePaths, progress, cancellationToken);
        }

        private static NamespaceReplaceRegex NoRegex()
        {
            return new NamespaceReplaceRegex(string.Empty, string.Empty);
        }

        /// <summary>
        /// A progress sink which reports synchronously, so a test may state what has been
        /// reported and in which order (<see cref="System.Progress{T}"/> of the framework
        /// posts to the synchronization context and gives no such guarantee).
        /// </summary>
        private sealed class ProgressRecorder : IProgress<AdjustProgress>
        {
            private readonly Action<AdjustProgress>? _onReport;

            /// <summary>
            /// Everything the session has reported, in the order it has been reported.
            /// </summary>
            public List<AdjustProgress> Reports
            {
                get;
            } = new List<AdjustProgress>();

            /// <param name="onReport">What the user does at that moment, if anything.</param>
            public ProgressRecorder(Action<AdjustProgress>? onReport = null)
            {
                _onReport = onReport;
            }

            /// <inheritdoc/>
            public void Report(AdjustProgress value)
            {
                Reports.Add(value);

                _onReport?.Invoke(value);
            }
        }
    }
}
