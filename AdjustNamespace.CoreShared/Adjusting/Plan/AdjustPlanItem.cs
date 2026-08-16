using AdjustNamespace.Namespace;
using System;
using System.Diagnostics;
using System.Collections.Generic;

namespace AdjustNamespace.Adjusting.Plan
{
    /// <summary>
    /// The decision to adjust one file: this file can be adjusted, and this is what has to
    /// happen with it. Produced by <see cref="AdjustPlanner"/> and executed by an
    /// <see cref="Adjuster.IAdjuster"/>.
    ///
    /// The separation matters because the decision is asked for twice: the second step of the
    /// wizard shows the user which files are going to change (see <see cref="SubjectFileCollector"/>)
    /// and the third step changes them (see <c>PerformingViewModel</c>). Both of them ask the
    /// very same planner now, so the wizard cannot offer a file the adjusting silently skips.
    /// </summary>
    [DebuggerDisplay("{FilePath} -> {TargetNamespace}")]
    public readonly struct AdjustPlanItem
    {
        /// <summary>
        /// Full path to the file to adjust.
        /// </summary>
        public readonly string FilePath;

        /// <summary>
        /// The namespace the types of that file have to be moved into.
        /// </summary>
        public readonly string TargetNamespace;

        /// <summary>
        /// The file is a xaml one, so <see cref="XamlAdjuster"/> takes it.
        /// </summary>
        public readonly bool IsXaml;

        /// <summary>
        /// The namespace transitions of the file (empty for a xaml one, which has no
        /// namespace declaration at all). They are a part of the decision: a file without
        /// a transition is not planned in the first place.
        /// </summary>
        public readonly NamespaceTransitionContainer Transitions;

        private AdjustPlanItem(
            string filePath,
            string targetNamespace,
            bool isXaml,
            NamespaceTransitionContainer transitions
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (targetNamespace is null)
            {
                throw new ArgumentNullException(nameof(targetNamespace));
            }

            FilePath = filePath;
            TargetNamespace = targetNamespace;
            IsXaml = isXaml;
            Transitions = transitions;
        }

        /// <summary>
        /// The decision for a xaml file: move the root class into the target namespace.
        /// </summary>
        public static AdjustPlanItem Xaml(
            string filePath,
            string targetNamespace
            )
        {
            return new AdjustPlanItem(
                filePath,
                targetNamespace,
                true,
                new NamespaceTransitionContainer(new List<NamespaceTransition>())
                );
        }

        /// <summary>
        /// The decision for a C# file: perform the given namespace transitions.
        /// </summary>
        public static AdjustPlanItem Cs(
            string filePath,
            string targetNamespace,
            NamespaceTransitionContainer transitions
            )
        {
            return new AdjustPlanItem(
                filePath,
                targetNamespace,
                false,
                transitions
                );
        }
    }
}
