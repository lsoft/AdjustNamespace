using AdjustNamespace.Roslyn;
using AdjustNamespace.Namespace;
using AdjustNamespace.VisualStudio;
using AdjustNamespace.Xaml;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Plan
{
    /// <summary>
    /// The single place which decides whether a file can and should be adjusted, and what
    /// exactly has to happen with it (see <see cref="AdjustPlanItem"/>).
    ///
    /// This decision used to be written twice: the second step of the wizard
    /// (<see cref="SubjectFileCollector"/>) made it to show the user the files which are
    /// going to change, and the adjusters made it again while the third step was running
    /// and silently did nothing when they disagreed. Now the adjusters execute a plan
    /// and never refuse it.
    ///
    /// A file the adjusting cannot process safely comes back as an <see cref="AdjustBlock"/>
    /// with a reason the wizard and the console utility show to the user. A file which is
    /// already in the right namespace is dropped silently (<see cref="AdjustPlanResult.None"/>).
    /// </summary>
    public sealed class AdjustPlanner
    {
        private readonly AdjustContext _context;
        private readonly NamespaceReplaceRegex _replaceRegex;

        /// <summary>
        /// The project of every file of the solution. The solution tree is expensive to walk
        /// (and is available from the main thread only), so it is asked for once per planner
        /// and not once per file.
        /// </summary>
        private IReadOnlyDictionary<string, ProjectRef>? _projectOfFile;

        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        public AdjustPlanner(
            AdjustContext context,
            NamespaceReplaceRegex replaceRegex
            )
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            _context = context;
            _replaceRegex = replaceRegex;
        }

        /// <summary>
        /// Decide what to do with the given file, with a block reason when the file
        /// cannot be adjusted safely.
        /// </summary>
        public async Task<AdjustPlanResult> PlanAsync(
            string subjectFilePath,
            CancellationToken cancellationToken = default
            )
        {
            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            var project = await TryGetProjectOfAsync(subjectFilePath);
            if (!project.HasValue)
            {
                return AdjustPlanResult.ForBlock(
                    AdjustBlock.Create(subjectFilePath, AdjustBlockKind.NoProject)
                    );
            }

            //several projects must be detected before the target namespace: a shared file
            //often lies outside of every referencing project's folder, and that would look
            //like an unknown target namespace instead of the real reason
            if (XamlPathHelper.IsXamlFile(subjectFilePath))
            {
                if (_context.Workspace.IsCompiledBySeveralProjects(subjectFilePath + ".cs"))
                {
                    return AdjustPlanResult.ForBlock(
                        AdjustBlock.Create(subjectFilePath, AdjustBlockKind.XamlCodeBehindMultiProject)
                        );
                }
            }
            else if (_context.Workspace.IsCompiledBySeveralProjects(subjectFilePath))
            {
                return AdjustPlanResult.ForBlock(
                    AdjustBlock.Create(subjectFilePath, AdjustBlockKind.CompiledBySeveralProjects)
                    );
            }

            var targetNamespace = await _context.TargetNamespaces.TryResolveAsync(
                project.Value,
                _replaceRegex,
                subjectFilePath
                );
            if (string.IsNullOrEmpty(targetNamespace))
            {
                return AdjustPlanResult.ForBlock(
                    AdjustBlock.Create(subjectFilePath, AdjustBlockKind.TargetNamespaceUnknown)
                    );
            }

            return await PlanAsync(
                _context.Workspace,
                subjectFilePath,
                targetNamespace!,
                cancellationToken
                );
        }

        /// <summary>
        /// Decide what to do with the given file.
        /// </summary>
        /// <returns>
        /// The decision, or <c>null</c> if the file is not adjusted (blocked or already fine).
        /// Prefer <see cref="PlanAsync(string, CancellationToken)"/> when the reason matters.
        /// </returns>
        public async Task<AdjustPlanItem?> TryPlanAsync(
            string subjectFilePath,
            CancellationToken cancellationToken = default
            )
        {
            var result = await PlanAsync(subjectFilePath, cancellationToken);
            return result.Plan;
        }

        /// <summary>
        /// The part of the decision which needs the Roslyn workspace only, i.e. everything
        /// except the target namespace of the file.
        /// </summary>
        public static async Task<AdjustPlanResult> PlanAsync(
            Workspace workspace,
            string subjectFilePath,
            string targetNamespace,
            CancellationToken cancellationToken = default
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            if (targetNamespace is null)
            {
                throw new ArgumentNullException(nameof(targetNamespace));
            }

            if (XamlPathHelper.IsXamlFile(subjectFilePath))
            {
                return PlanXaml(workspace, subjectFilePath, targetNamespace);
            }

            return await PlanCsAsync(workspace, subjectFilePath, targetNamespace, cancellationToken);
        }

        /// <summary>
        /// The part of the decision which needs the Roslyn workspace only.
        /// </summary>
        /// <returns>
        /// The decision, or <c>null</c> if the file has to be left alone.
        /// Prefer <see cref="PlanAsync(Workspace, string, string, CancellationToken)"/>
        /// when the reason matters.
        /// </returns>
        public static async Task<AdjustPlanItem?> TryPlanAsync(
            Workspace workspace,
            string subjectFilePath,
            string targetNamespace,
            CancellationToken cancellationToken = default
            )
        {
            var result = await PlanAsync(workspace, subjectFilePath, targetNamespace, cancellationToken);
            return result.Plan;
        }

        private static AdjustPlanResult PlanXaml(
            Workspace workspace,
            string subjectFilePath,
            string targetNamespace
            )
        {
            //the `x:Class` of a xaml and the namespace of its code behind file are the two
            //halves of one class. The code behind file is a usual C# file and is skipped when
            //several projects compile it, so this file has to be skipped too
            if (workspace.IsCompiledBySeveralProjects(subjectFilePath + ".cs"))
            {
                return AdjustPlanResult.ForBlock(
                    AdjustBlock.Create(subjectFilePath, AdjustBlockKind.XamlCodeBehindMultiProject)
                    );
            }

            return AdjustPlanResult.ForPlan(
                AdjustPlanItem.Xaml(subjectFilePath, targetNamespace)
                );
        }

        private static async Task<AdjustPlanResult> PlanCsAsync(
            Workspace workspace,
            string subjectFilePath,
            string targetNamespace,
            CancellationToken cancellationToken
            )
        {
            //we can do nothing with not a C# document
            if (!workspace.GetDocument(subjectFilePath).IsDocumentInScope())
            {
                return AdjustPlanResult.ForBlock(
                    AdjustBlock.Create(subjectFilePath, AdjustBlockKind.NotAProcessableDocument)
                    );
            }

            if (workspace.IsCompiledBySeveralProjects(subjectFilePath))
            {
                //a file of a shared project which is referenced by several projects:
                //there is no target namespace which suits all of them
                return AdjustPlanResult.ForBlock(
                    AdjustBlock.Create(subjectFilePath, AdjustBlockKind.CompiledBySeveralProjects)
                    );
            }

            //a file may be compiled by several projects (the target frameworks of a multi target
            //project), and every one of them parses it with its own conditional compilation
            //symbols: a namespace declared under such a symbol exists in a part of these trees
            //only, so all of them are taken into account
            var transitions = NamespaceTransitionContainer.GetNamespaceTransitionsFor(
                await workspace.GetSyntaxRootsAsync(subjectFilePath, cancellationToken),
                targetNamespace
                );
            if (transitions.IsEmpty)
            {
                //nothing to move: the file is in the target namespace already
                return AdjustPlanResult.None();
            }

            foreach (var transition in transitions.Transitions)
            {
                if (await workspace.IsNamespaceStateContradictoryAsync(subjectFilePath, transition.OriginalName, cancellationToken))
                {
                    //the projects which compile this file do not agree whether the namespace
                    //it is moved out of stays alive, and there is a single text for all of them:
                    //whatever we do with the using clauses, one of these projects breaks
                    return AdjustPlanResult.ForBlock(
                        AdjustBlock.Create(subjectFilePath, AdjustBlockKind.NamespaceStateContradictory)
                        );
                }
            }

            return AdjustPlanResult.ForPlan(
                AdjustPlanItem.Cs(subjectFilePath, targetNamespace, transitions)
                );
        }

        /// <summary>
        /// The project the file belongs to, out of the solution tree fetched once,
        /// see <see cref="_projectOfFile"/>.
        /// </summary>
        private async Task<ProjectRef?> TryGetProjectOfAsync(
            string filePath
            )
        {
            if (_projectOfFile == null)
            {
                _projectOfFile = await _context.Solution.GetProjectOfEveryFileAsync();
            }

            if (!_projectOfFile.TryGetValue(filePath, out var project))
            {
                return null;
            }

            return project;
        }
    }
}
