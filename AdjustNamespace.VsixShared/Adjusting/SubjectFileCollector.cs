using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace.Roslyn;
using AdjustNamespace.Namespace;
using AdjustNamespace.UI;
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
    /// a conflict makes the adjusting impossible and has to be reported before anything
    /// has been changed.
    ///
    /// Used by the second step of the wizard.
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
        /// Analyze the incoming files and collect those of them which are the subject to change.
        /// </summary>
        /// <param name="progressMessageAction">Progress callback: (processed file index, total file count, current file path).</param>
        /// <exception cref="FileProcessException">
        /// The target namespace of a file already contains a type with the same name,
        /// or the file cannot be processed at all.
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

            var total = subjectFilePaths.Count;
            for (int i = 0; i < total; i++)
            {
                var subjectFilePath = subjectFilePaths[i];

                progressMessageAction(i + 1, total, subjectFilePath);

                var plan = await TryPlanAsync(planner, subjectFilePath);
                if (!plan.HasValue)
                {
                    continue;
                }

                if (plan.Value.IsXaml)
                {
                    //a xaml file is planned as soon as it belongs to a project, and whether
                    //its root class really moves is known after the document has been read
                    var xamlAdjuster = new XamlAdjuster(false, plan.Value);
                    if (!await xamlAdjuster.IsChangesExistsAsync())
                    {
                        continue;
                    }
                }
                else
                {
                    await CheckForTypeNameConflictsAsync(
                        plan.Value,
                        typesInSolutionPerNamespace
                        );
                }

                foundFileExs.Add(
                    new FileEx(subjectFilePath)
                    );
            }

            return new SubjectCollectingResults(foundFileExs);
        }

        /// <summary>
        /// The decision of the planner for a single file, with a failure of it bound
        /// to that file.
        /// </summary>
        /// <exception cref="FileProcessException">The file cannot be processed at all.</exception>
        private static async Task<AdjustPlanItem?> TryPlanAsync(
            AdjustPlanner planner,
            string subjectFilePath
            )
        {
            try
            {
                return await planner.TryPlanAsync(subjectFilePath);
            }
            catch (Exception ex)
            {
                throw new FileProcessException(subjectFilePath, ex);
            }
        }

        /// <summary>
        /// Check that no type of the file lands onto a type of the same name which exists
        /// in the target namespace already: such a move breaks the solution and cannot be
        /// undone by the adjusting itself.
        /// </summary>
        /// <exception cref="FileProcessException">There is such a type.</exception>
        private async Task CheckForTypeNameConflictsAsync(
            AdjustPlanItem plan,
            NamespaceTypeContainer typesInSolutionPerNamespace
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

                foreach (var foundType in syntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var symbolInfo = semanticModel.GetDeclaredSymbol(foundType);
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

                    if (typesInSolutionPerNamespace.CheckForTypeExists(transition.Value.ModifiedName, symbolInfo.Name))
                    {
                        throw new FileProcessException(
                            $"'{plan.TargetNamespace}' already contains a type '{symbolInfo.Name}'",
                            plan.FilePath
                            );
                    }
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

            public SubjectCollectingResults(
                List<FileEx> collectedFiles
                )
            {
                if (collectedFiles is null)
                {
                    throw new ArgumentNullException(nameof(collectedFiles));
                }

                CollectedFiles = collectedFiles;
            }
        }
    }

    /// <summary>
    /// An error which makes the processing of a specific file impossible.
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
