using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Roslyn;
using AdjustNamespace.Namespace;
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// Analyzer which filters the files chosen by the user and keeps only those
    /// which are really the subject to change.
    ///
    /// Whether a file is a subject to change at all is decided by <see cref="AdjustPlanner"/>,
    /// i.e. by the very same code the adjusting itself runs on. What is added here is what
    /// only this step needs: the type name conflicts in the target namespaces, because such
    /// a conflict makes the adjusting of that file impossible and has to be reported before
    /// anything has been changed. Other files of the same scan keep being collected.
    ///
    /// Used by the second step of the wizard and by the console utility.
    /// </summary>
    public sealed class SubjectFileCollector
    {
        private readonly AdjustContext _context;
        private readonly HashSet<string> _subjectFilePaths;
        private readonly NamespaceReplaceRegex _replaceRegex;

        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="subjectFilePaths">Full paths of the files chosen by the user.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        public SubjectFileCollector(
            AdjustContext context,
            HashSet<string> subjectFilePaths,
            NamespaceReplaceRegex replaceRegex
            )
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            _context = context;
            _subjectFilePaths = subjectFilePaths;
            _replaceRegex = replaceRegex;
        }

        /// <summary>
        /// Analyze the incoming files and collect those of them which are the subject to change,
        /// plus the ones which cannot be adjusted (with a reason for the user).
        /// </summary>
        /// <param name="progressMessageAction">Progress callback: (processed file index, total file count, current file path).</param>
        /// <exception cref="FileProcessException">
        /// An unexpected failure while deciding what to do with a file.
        /// </exception>
        public async Task<SubjectCollectingResults> AnalyzeAndCollectAsync(
            Action<int, int, string> progressMessageAction
            )
        {
            if (progressMessageAction is null)
            {
                throw new ArgumentNullException(nameof(progressMessageAction));
            }

            var planner = new AdjustPlanner(_context, _replaceRegex);

            // get all types in solution
            var typesInSolutionPerNamespace = await NamespaceTypeContainer.CreateForAsync(
                _context.Workspace
                );

            var subjectFilePaths = _subjectFilePaths.ToList();
            var foundFileExs = new List<FileEx>();
            var blocked = new List<AdjustBlock>();

            var total = subjectFilePaths.Count;
            for (int i = 0; i < total; i++)
            {
                var subjectFilePath = subjectFilePaths[i];

                progressMessageAction(i + 1, total, subjectFilePath);

                var result = await PlanAsync(planner, subjectFilePath);
                if (result.HasBlock)
                {
                    blocked.Add(result.Block!.Value);
                    continue;
                }

                if (!result.HasPlan)
                {
                    //already in the target namespace (or otherwise nothing to do)
                    continue;
                }

                var plan = result.Plan!.Value;

                if (plan.IsXaml)
                {
                    //a xaml file is planned as soon as it belongs to a project, and whether
                    //its root class really moves is known after the document has been read
                    var xamlAdjuster = new XamlAdjuster(
                        _context.XamlBodyProviderFactory,
                        plan
                        );
                    if (!await xamlAdjuster.IsChangesExistsAsync())
                    {
                        continue;
                    }
                }
                else
                {
                    var conflict = await TryGetTypeNameConflictAsync(
                        plan,
                        typesInSolutionPerNamespace
                        );
                    if (conflict.HasValue)
                    {
                        blocked.Add(conflict.Value);
                        continue;
                    }

                    //reserve the types this file is about to place into the target, so the
                    //next subject file which would land the same name there conflicts with
                    //this one and not only with the types which exist already
                    await ReserveMovingTypesAsync(
                        plan,
                        typesInSolutionPerNamespace
                        );
                }

                foundFileExs.Add(
                    new FileEx(subjectFilePath)
                    );
            }

            return new SubjectCollectingResults(foundFileExs, blocked);
        }

        /// <summary>
        /// The decision of the planner for a single file, with an unexpected failure of it
        /// bound to that file.
        /// </summary>
        /// <exception cref="FileProcessException">The file cannot be processed at all.</exception>
        private static async Task<AdjustPlanResult> PlanAsync(
            AdjustPlanner planner,
            string subjectFilePath
            )
        {
            try
            {
                return await planner.PlanAsync(subjectFilePath);
            }
            catch (Exception ex)
            {
                throw new FileProcessException(subjectFilePath, ex);
            }
        }

        /// <summary>
        /// The first type of the file which would land onto a type of the same name in the
        /// target namespace, or <c>null</c> if there is no such conflict.
        /// </summary>
        private async Task<AdjustBlock?> TryGetTypeNameConflictAsync(
            AdjustPlanItem plan,
            NamespaceTypeContainer typesInSolutionPerNamespace
            )
        {
            AdjustBlock? conflict = null;

            await ForEachMovingTypeAsync(
                plan,
                (symbolInfo, transition) =>
                {
                    if (conflict.HasValue)
                    {
                        return Task.CompletedTask;
                    }

                    if (typesInSolutionPerNamespace.CheckForTypeExists(transition.ModifiedName, symbolInfo.Name))
                    {
                        conflict = AdjustBlock.TypeNameConflict(
                            plan.FilePath,
                            transition.ModifiedName,
                            symbolInfo.Name
                            );
                    }

                    return Task.CompletedTask;
                }
                );

            return conflict;
        }

        /// <summary>
        /// Record the types of the file under the namespaces they will land in, so the
        /// next file of the same scan sees them as occupants of those namespaces.
        /// </summary>
        private async Task ReserveMovingTypesAsync(
            AdjustPlanItem plan,
            NamespaceTypeContainer typesInSolutionPerNamespace
            )
        {
            await ForEachMovingTypeAsync(
                plan,
                (symbolInfo, transition) =>
                {
                    typesInSolutionPerNamespace.Reserve(transition.ModifiedName, symbolInfo.Name);
                    return Task.CompletedTask;
                }
                );
        }

        /// <summary>
        /// Walk every top-level type of the file which is going to move, with the
        /// transition of the declaration it is written in.
        /// </summary>
        private async Task ForEachMovingTypeAsync(
            AdjustPlanItem plan,
            Func<INamedTypeSymbol, NamespaceTransition, Task> action
            )
        {
            //every project which compiles the file is asked: a type declared under
            //a conditional compilation symbol exists in a part of them only
            foreach (var fileDocument in _context.Workspace.GetDocuments(plan.FilePath))
            {
                var semanticModel = await fileDocument.GetSemanticModelAsync();
                if (semanticModel == null)
                {
                    continue;
                }

                var syntaxRoot = await fileDocument.GetSyntaxRootAsync();
                if (syntaxRoot == null)
                {
                    continue;
                }

                //the same kinds CsAdjuster moves: classes/structs/interfaces/records,
                //enums and delegates. TypeDeclarationSyntax alone misses the last two,
                //and a name conflict of either of them is just as fatal (CS0101).
                var foundTypes = (
                    from snode in syntaxRoot.DescendantNodes()
                    where snode is TypeDeclarationSyntax || snode is EnumDeclarationSyntax || snode is DelegateDeclarationSyntax
                    select snode
                    );

                foreach (var foundType in foundTypes)
                {
                    var symbolInfo = semanticModel.GetDeclaredSymbol(foundType) as INamedTypeSymbol;
                    if (symbolInfo == null)
                    {
                        continue;
                    }

                    if (symbolInfo.ContainingType != null)
                    {
                        //a nested type moves together with its outer type
                        //and never conflicts with a type of the target namespace
                        continue;
                    }

                    var symbolNamespace = symbolInfo.ContainingNamespace.ToDisplayString();
                    if (symbolNamespace == plan.TargetNamespace)
                    {
                        continue;
                    }

                    if (NamespaceHelper.IsSpecialNamespace(symbolNamespace))
                    {
                        continue;
                    }

                    //the transition of the very declaration this type is written in,
                    //exactly as CsAdjuster does it
                    var transition = NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf(
                        foundType,
                        plan.TargetNamespace
                        );
                    if (!transition.HasValue)
                    {
                        //there is no transition for this type: it is declared
                        //outside of any namespace, for example. It is not moved at all.
                        continue;
                    }

                    await action(symbolInfo, transition.Value);
                }
            }
        }

        /// <summary>
        /// Results of <see cref="AnalyzeAndCollectAsync"/>.
        /// </summary>
        public sealed class SubjectCollectingResults
        {
            /// <summary>
            /// Files which are the subject to change.
            /// </summary>
            public List<FileEx> CollectedFiles
            {
                get;
            }

            /// <summary>
            /// Files which cannot be adjusted, with a reason for the user.
            /// </summary>
            public IReadOnlyList<AdjustBlock> Blocked
            {
                get;
            }

            public SubjectCollectingResults(
                List<FileEx> collectedFiles,
                IReadOnlyList<AdjustBlock> blocked
                )
            {
                if (collectedFiles is null)
                {
                    throw new ArgumentNullException(nameof(collectedFiles));
                }

                if (blocked is null)
                {
                    throw new ArgumentNullException(nameof(blocked));
                }

                CollectedFiles = collectedFiles;
                Blocked = blocked;
            }
        }
    }

    /// <summary>
    /// An unexpected error which makes the processing of a specific file impossible.
    /// Type name conflicts and other known blocks are <see cref="AdjustBlock"/> instead.
    /// </summary>
    public sealed class FileProcessException : Exception
    {
        /// <summary>
        /// Full path to the file the error is related to.
        /// </summary>
        public string FilePath
        {
            get;
        }

        /// <param name="message">Error description shown to the user.</param>
        /// <param name="filePath">Full path to the problem file.</param>
        public FileProcessException(
            string message,
            string filePath
            )
            : base(message)
        {
            FilePath = filePath;
        }

        /// <param name="filePath">Full path to the problem file.</param>
        /// <param name="ex">The original error.</param>
        public FileProcessException(
            string filePath,
            Exception ex
            )
            : base($"Processing of {filePath} failed", ex)
        {
            FilePath = filePath;
        }
    }

}
