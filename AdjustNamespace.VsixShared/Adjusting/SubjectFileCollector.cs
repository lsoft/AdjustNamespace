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
    public sealed class SubjectFileCollector
    {
        private readonly VsServices _vss;
        private readonly HashSet<string> _subjectFilePaths;
        private readonly NamespaceReplaceRegex _replaceRegex;

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

                    var subjectSemanticModel = await subjectDocument!.GetSemanticModelAsync();
                    if (subjectSemanticModel == null)
                    {
                        continue;
                    }

                    var subjectSyntaxRoot = await subjectDocument.GetSyntaxRootAsync();
                    if (subjectSyntaxRoot == null)
                    {
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

                    var ntc = NamespaceTransitionContainer.GetNamespaceTransitionsFor(subjectSyntaxRoot, targetNamespace!);
                    if (ntc.IsEmpty)
                    {
                        continue;
                    }

                    //check for same types already exists in the destination namespace
                    foreach (var foundType in subjectSyntaxRoot.DescendantNodes().OfType<TypeDeclarationSyntax>())
                    {
                        var symbolInfo = subjectSemanticModel.GetDeclaredSymbol(foundType);
                        if (symbolInfo == null)
                        {
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

                        var targetNamespaceInfo = ntc.TransitionDict[symbolNamespace];
                        if (typesInSolutionPerNamespace.CheckForTypeExists(targetNamespaceInfo.ModifiedName, symbolInfo.Name))
                        {
                            throw new FileProcessException(
                                $"'{targetNamespace}' already contains a type '{symbolInfo.Name}'",
                                subjectFilePath
                                );
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


        public sealed class SubjectCollectingResults
        {
            //public bool IsOk => string.IsNullOrEmpty(ErrorMessage);

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
            public readonly string FilePath;
            public readonly SolutionItem Project;
            public readonly string ProjectPath;
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

    public sealed class FileProcessException : Exception
    {
        public string FilePath
        {
            get;
        }

        public FileProcessException(
            string message,
            string filePath
            )
            : base(message)
        {
            FilePath = filePath;
        }

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