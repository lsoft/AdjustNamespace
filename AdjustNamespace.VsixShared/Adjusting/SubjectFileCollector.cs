using AdjustNamespace.Helper;
using AdjustNamespace.Namespace;
using AdjustNamespace.UI.ViewModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// Analyzer which filters the files chosen by the user and keeps only those
    /// which are really the subject to change (its namespace differs from the target one).
    /// It also detects the type name conflicts in the target namespaces in advance,
    /// because such a conflict makes the adjusting impossible.
    /// Used by the second step of the wizard.
    /// </summary>
    public sealed class SubjectFileCollector
    {
        private readonly VsServices _vss;
        private readonly HashSet<string> _subjectFilePaths;
        private readonly NamespaceReplaceRegex _replaceRegex;

        /// <param name="vss">Visual Studio services.</param>
        /// <param name="subjectFilePaths">Full paths of the files chosen by the user.</param>
        /// <param name="replaceRegex">User defined regex which additionally modifies the target namespace.</param>
        public SubjectFileCollector(
            VsServices vss,
            HashSet<string> subjectFilePaths,
            NamespaceReplaceRegex replaceRegex
            )
        {
            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            if (replaceRegex is null)
            {
                throw new ArgumentNullException(nameof(replaceRegex));
            }

            _vss = vss;
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

            //try
            //{


            //extract project items (we need perform this in main thread)
            var fileExtensions = await BuildFileExtensionAsync();

            var foundFileExs = new List<FileEx>();

            // get all types in solution
            var typesInSolutionPerNamespace = await NamespaceTypeContainer.CreateForAsync(
                _vss.Workspace
                );

            var total = fileExtensions.Count;
            for (int i = 0; i < total; i++)
            {
                FileExtension fileExtension = fileExtensions[i];
                var subjectFilePath = fileExtension.FilePath;
                var subjectProject = fileExtension.Project;
                //var subjectProjectItem = fileExtension.ProjectItem;

                progressMessageAction(i + 1, total, subjectFilePath);

                if (subjectFilePath.EndsWith(".xaml"))
                {
                    //TODO: unify create XamlAdjuster across the VSIX codebase
                    var targetNamespace = await NamespaceHelper.TryDetermineTargetNamespaceAsync(
                        subjectProject,
                        _vss,
                        _replaceRegex,
                        subjectFilePath
                        );
                    if (!string.IsNullOrEmpty(targetNamespace))
                    {
                        var xamlAdjuster = new XamlAdjuster(
                            _vss,
                            false,
                            subjectFilePath,
                            targetNamespace!
                            );
                        if (await xamlAdjuster.IsChangesExistsAsync())
                        {
                            foundFileExs.Add(
                                new FileEx(fileExtension.FilePath, fileExtension.ProjectPath)
                                );
                        }
                    }

                    continue;
                }
                else
                {
                    var subjectDocument = _vss.Workspace.GetDocument(subjectFilePath);
                    if (!subjectDocument.IsDocumentInScope())
                    {
                        continue;
                    }

                    if (_vss.Workspace.IsCompiledBySeveralProjects(subjectFilePath))
                    {
                        //a file of a shared project which is referenced by several projects:
                        //there is no target namespace which suits all of them, see CsAdjuster
                        continue;
                    }

                    var targetNamespace = await NamespaceHelper.TryDetermineTargetNamespaceAsync(
                        subjectProject,
                        _vss,
                        _replaceRegex,
                        subjectFilePath
                        );
                    if (string.IsNullOrEmpty(targetNamespace))
                    {
                        continue;
                    }

                    var ntc = NamespaceTransitionContainer.GetNamespaceTransitionsFor(
                        await _vss.Workspace.GetSyntaxRootsAsync(subjectFilePath),
                        targetNamespace!
                        );
                    if (ntc.IsEmpty)
                    {
                        continue;
                    }

                    var isContradictory = false;
                    foreach (var transition in ntc.Transitions)
                    {
                        if (await _vss.Workspace.IsNamespaceStateContradictoryAsync(subjectFilePath, transition.OriginalName))
                        {
                            //the projects which compile this file do not agree whether the
                            //namespace it is moved out of stays alive, see CsAdjuster
                            isContradictory = true;
                            break;
                        }
                    }

                    if (isContradictory)
                    {
                        continue;
                    }

                    //check for same types already exists in the destination namespace
                    //(every project which compiles the file is asked: a type declared under
                    //a conditional compilation symbol exists in a part of them only)
                    foreach (var fileDocument in _vss.Workspace.GetDocuments(subjectFilePath))
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
                            if (symbolNamespace == targetNamespace)
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
                                targetNamespace!
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
                                    $"'{targetNamespace}' already contains a type '{symbolInfo.Name}'",
                                    subjectFilePath
                                    );
                            }
                        }
                    }

                    foundFileExs.Add(
                        new FileEx(fileExtension.FilePath, fileExtension.ProjectPath)
                        );
                }
            }

            return new SubjectCollectingResults(foundFileExs);

            //}
            //catch (Exception excp)
            //{
            //    await AddMessageAsync(
            //        excp.Message
            //        );
            //    await AddMessageAsync(
            //        excp.StackTrace
            //        );

            //    Logging.LogVS(excp);
            //}

            //return [];
        }

        /// <summary>
        /// Bind the incoming file paths to their projects and project items.
        /// DTE/solution objects are available from the main thread only, that's why
        /// this is performed as a single separate step.
        /// </summary>
        private async Task<List<FileExtension>> BuildFileExtensionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var fileExtensions = new List<FileExtension>(
                _subjectFilePaths.Count
                );

            var projectItems = await SolutionHelper.GetAllProjectItemsAsync(null);
            foreach (var filePath in _subjectFilePaths)
            {
                try
                {
                    if (!projectItems.TryGetValue(filePath, out var pii))
                    {
                        continue;
                    }

                    fileExtensions.Add(
                        new FileExtension(
                            filePath,
                            pii.Project,
                            pii.ProjectItem
                            )
                        );
                }
                catch (Exception ex)
                {
                    throw new FileProcessException(filePath, ex);
                }
            }

            return fileExtensions;
        }


        /// <summary>
        /// Results of <see cref="AnalyzeAndCollectAsync"/>.
        /// </summary>
        public sealed class SubjectCollectingResults
        {
            //public bool IsOk => string.IsNullOrEmpty(ErrorMessage);

            /// <summary>
            /// Files which are the subject to change.
            /// </summary>
            public List<FileEx> CollectedFiles
            {
                get;
            }

            //public string? ErrorMessage
            //{
            //    get;
            //}

            //public SubjectCollectingResults(
            //    string errorMessage
            //    )
            //{
            //    if (errorMessage is null)
            //    {
            //        throw new ArgumentNullException(nameof(errorMessage));
            //    }

            //    CollectedFiles = [];
            //    ErrorMessage = errorMessage;
            //}

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

        /// <summary>
        /// Extension for file from workspace.
        /// </summary>
        [DebuggerDisplay("{FilePath}")]
        private readonly struct FileExtension
        {
            /// <summary>
            /// Full path to the file.
            /// </summary>
            public readonly string FilePath;

            /// <summary>
            /// Project the file belongs to.
            /// </summary>
            public readonly SolutionItem Project;

            /// <summary>
            /// Full path to the project file.
            /// </summary>
            public readonly string ProjectPath;

            /// <summary>
            /// Project item of the file.
            /// </summary>
            public readonly SolutionItem ProjectItem;

            public FileExtension(
                string filePath,
                SolutionItem project,
                SolutionItem projectItem
                )
            {
                if (project is null)
                {
                    throw new ArgumentNullException(nameof(project));
                }

                if (projectItem is null)
                {
                    throw new ArgumentNullException(nameof(projectItem));
                }
                if (filePath is null)
                {
                    throw new ArgumentNullException(nameof(filePath));
                }

                FilePath = filePath;
                Project = project;
                ProjectPath = project.FullPath!;
                ProjectItem = projectItem;
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