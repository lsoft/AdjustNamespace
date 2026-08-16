using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Roslyn
{
    /// <summary>
    /// Extensions which ask the Roslyn workspace about the documents of a file.
    /// Partially taken from  https://github.com/bert2/microscope.
    /// Take a look to that repo, it's amazing!
    /// </summary>
    public static class WorkspaceExtensions
    {
        private static readonly MethodInfo _getDocumentIdInCurrentContextMethod = typeof(Workspace).GetMethod(
            "GetDocumentIdInCurrentContext",
            BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null,
            types: new[] { typeof(DocumentId) },
            modifiers: null)!;

        /// <summary>
        /// Full paths of all the documents of the workspace which pass the given filters.
        /// A file which several projects compile (a shared project, a multi target project)
        /// is a single file on the disk and is reported once,
        /// see <see cref="IsCompiledBySeveralProjects"/>.
        /// </summary>
        /// <param name="projectPredicate">Project filter, see <see cref="Scope.IsProjectInScope"/>.</param>
        /// <param name="documentPredicate">Document filter, see <see cref="Scope.IsDocumentInScope"/>.</param>
        public static IReadOnlyList<string> EnumerateAllDocumentFilePaths(
            this Workspace workspace,
            Func<Microsoft.CodeAnalysis.Project, bool> projectPredicate,
            Func<Document, bool> documentPredicate
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (projectPredicate is null)
            {
                throw new ArgumentNullException(nameof(projectPredicate));
            }

            if (documentPredicate is null)
            {
                throw new ArgumentNullException(nameof(documentPredicate));
            }

            var result = new List<string>();
            var addedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var project in workspace.CurrentSolution.Projects)
            {
                if (!projectPredicate(project))
                {
                    continue;
                }

                foreach (var document in project.Documents)
                {
                    if (!documentPredicate(document))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(document.FilePath))
                    {
                        if (!addedFilePaths.Add(document.FilePath!))
                        {
                            //that file is compiled by another project of the solution too
                            continue;
                        }

                        result.Add(document.FilePath!);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// The file is compiled by more than one project of the solution, i.e. it is a file
        /// of a shared project (.shproj) which is referenced by several projects.
        ///
        /// The namespace of a file is derived from the project it belongs to
        /// (see <see cref="Namespace.TargetNamespaceResolver"/>), and every one
        /// of these projects gives another answer, so there is no namespace such a file could
        /// be adjusted to and it has to be left as it is.
        ///
        /// A multi target project (<c>net48;net8.0</c>) is NOT such a case: Visual Studio
        /// creates a Roslyn project per target framework and all of them compile the very same
        /// file, but all of them are the same project of the solution, hence the comparison
        /// of the project files and not of the project count.
        /// </summary>
        public static bool IsCompiledBySeveralProjects(
            this Workspace workspace,
            string filePath
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            var sln = workspace.CurrentSolution;

            var projectFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var documentId in sln.GetDocumentIdsWithFilePath(filePath))
            {
                var project = sln.GetProject(documentId.ProjectId);
                if (project == null)
                {
                    continue;
                }

                projectFilePaths.Add(project.FilePath ?? project.Name);

                if (projectFilePaths.Count > 1)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// All the documents built of the given file: one per project which compiles it,
        /// see <see cref="IsCompiledBySeveralProjects"/>.
        ///
        /// There is a single file on the disk and a single text behind all of them, but their
        /// syntax trees are not necessarily the same: every target framework of a multi target
        /// project defines its own conditional compilation symbols, so a fragment which is
        /// a code for one of these documents is a disabled text (i.e. a trivia) for another one.
        /// </summary>
        public static IReadOnlyList<Document> GetDocuments(
            this Workspace workspace,
            string filePath
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            var sln = workspace.CurrentSolution;

            var result = new List<Document>();

            foreach (var documentId in sln.GetDocumentIdsWithFilePath(filePath))
            {
                var document = sln.GetDocument(documentId);
                if (document == null)
                {
                    continue;
                }

                result.Add(document);
            }

            return result;
        }

        /// <summary>
        /// The syntax roots of all the documents of the file, see <see cref="GetDocuments"/>.
        /// The documents without a syntax tree are skipped.
        /// </summary>
        public static async Task<IReadOnlyList<SyntaxNode>> GetSyntaxRootsAsync(
            this Workspace workspace,
            string filePath,
            CancellationToken cancellationToken = default
            )
        {
            var result = new List<SyntaxNode>();

            foreach (var document in workspace.GetDocuments(filePath))
            {
                var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                if (syntaxRoot == null)
                {
                    continue;
                }

                result.Add(syntaxRoot);
            }

            return result;
        }

        /// <summary>
        /// The namespace still contains something for at least one of the projects which
        /// compile the given file, after that file has been moved out of it
        /// (see <see cref="SymbolExtensions.IsNamespaceFilledOutside"/>).
        /// </summary>
        public static async Task<bool> IsNamespaceAliveOutsideAsync(
            this Workspace workspace,
            string filePath,
            string namespaceName
            )
        {
            foreach (var document in workspace.GetDocuments(filePath))
            {
                var compilation = await document.Project.GetCompilationAsync();
                if (compilation == null)
                {
                    //we know nothing, so keep the old behaviour
                    return true;
                }

                if (compilation.IsNamespaceFilledOutside(namespaceName, filePath))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The projects which compile the given file do not agree whether the given namespace
        /// still exists after that file has been moved out of it: another file fills it for
        /// one of them and it becomes empty for another one.
        ///
        /// The `using` clause of such a namespace is required by the first ones and does not
        /// compile for the second ones, and there is a single text for all of them, so there
        /// is no correct way to move that file at all and it has to be left as it is.
        /// This happens when a type of that namespace is declared under a conditional
        /// compilation symbol of a target framework, or in a file of a single target framework.
        /// </summary>
        public static async Task<bool> IsNamespaceStateContradictoryAsync(
            this Workspace workspace,
            string filePath,
            string namespaceName,
            CancellationToken cancellationToken = default
            )
        {
            bool? firstAnswer = null;

            foreach (var document in workspace.GetDocuments(filePath))
            {
                var compilation = await document.Project.GetCompilationAsync(cancellationToken);
                if (compilation == null)
                {
                    continue;
                }

                var isAlive = compilation.IsNamespaceFilledOutside(namespaceName, filePath);

                if (!firstAnswer.HasValue)
                {
                    firstAnswer = isAlive;
                    continue;
                }

                if (firstAnswer.Value != isAlive)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the document and its syntax root from the current solution snapshot.
        /// </summary>
        /// <returns><c>(null, null)</c> if the document is not found or has no syntax tree.</returns>
        public static async Task<(Document?, SyntaxNode?)> GetDocumentAndSyntaxRootAsync(
            this Workspace workspace,
            string filePath,
            CancellationToken cancellationToken = default
            )
        {
            var document = workspace.GetDocument(filePath);
            if (document == null)
            {
                //skip this document
                return (null, null);
            }

            var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
            if (syntaxRoot == null)
            {
                //skip this document
                return (null, null);
            }

            return (document, syntaxRoot);
        }

        /// <summary>
        /// Get the document by its file path from the current solution snapshot.
        /// </summary>
        /// <returns><c>null</c> if there is no such document in the workspace.</returns>
        public static Document? GetDocument(this Workspace workspace, string filePath)
        {
            var sln = workspace.CurrentSolution;

            var candidateId = sln
                .GetDocumentIdsWithFilePath(filePath)
                // VS will create multiple `ProjectId`s for projects with multiple target frameworks.
                // We simply take the first one we find.
                .FirstOrDefault()
                ;
            if (candidateId == null)
            {
                return null;
            }

            var currentContextId = workspace.GetDocumentIdInCurrentContext(candidateId);

            return sln.GetDocument(currentContextId);
        }

        /// <summary>
        /// Resolve the document id which corresponds to the current context of a linked document
        /// (a file shared between the projects or the targets of a multi-target project).
        /// The corresponding Roslyn method is internal, hence the reflection.
        /// </summary>
        public static DocumentId? GetDocumentIdInCurrentContext(
            this Workspace workspace,
            DocumentId? documentId
            )
        {
            return
                (DocumentId?)_getDocumentIdInCurrentContextMethod.Invoke(workspace, new[] { documentId });
        }
    }
}
