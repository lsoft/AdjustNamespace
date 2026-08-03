using AdjustNamespace.Settings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AdjustNamespace.Tests.Infrastructure
{
    /// <summary>
    /// A solution built in memory over an <see cref="AdhocWorkspace"/>.
    ///
    /// The core of the extension (the adjusters, the fixers and the cleanup) needs nothing
    /// but a Roslyn workspace, so it can be tested here without Visual Studio at all;
    /// see <see cref="VsServices.CreateForTests"/>.
    ///
    /// The C# documents live in the workspace only and are never written to the disk
    /// (their paths point into <see cref="SolutionFolder"/> but no file is created).
    /// The xaml files are real files, because the xaml subsystem works with the file system.
    /// </summary>
    public sealed class TestSolution : IDisposable
    {
        private static readonly IReadOnlyList<MetadataReference> _metadataReferences =
            new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location),
            };

        private readonly Dictionary<string, ProjectId> _projects = new();
        private readonly Dictionary<string, DocumentId> _documents = new();

        /// <summary>
        /// The workspace the solution lives in.
        /// </summary>
        public AdhocWorkspace Workspace
        {
            get;
        }

        /// <summary>
        /// Folder of the solution. It exists on the disk and is removed on <see cref="Dispose"/>.
        /// </summary>
        public string SolutionFolder
        {
            get;
        }

        /// <summary>
        /// Settings of this solution.
        /// </summary>
        public AdjustNamespaceSettings2 Settings
        {
            get;
            private set;
        }

        /// <summary>
        /// Visual Studio services over this solution (without any real Visual Studio behind).
        /// </summary>
        public VsServices Services => VsServices.CreateForTests(Workspace, Settings);

        public TestSolution()
        {
            SolutionFolder = Path.Combine(
                Path.GetTempPath(),
                "AdjustNamespace.Tests",
                Guid.NewGuid().ToString("N")
                );
            Directory.CreateDirectory(SolutionFolder);

            Workspace = new AdhocWorkspace();
            Settings = new AdjustNamespaceSettings2(
                SolutionFolder,
                new AdjustNamespaceSettings()
                );
        }

        /// <summary>
        /// Exclude the given folders from the namespace chain, as the user does it
        /// through the settings file.
        /// </summary>
        /// <param name="folders">Rooted paths or paths relative to the solution folder.</param>
        public TestSolution WithSkippedFolders(params string[] folders)
        {
            Settings = new AdjustNamespaceSettings2(
                SolutionFolder,
                new AdjustNamespaceSettings
                {
                    SkippedFolderSuffixes = folders.ToList()
                }
                );

            return this;
        }

        /// <summary>
        /// Add an empty C# project. Its folder is <c>{SolutionFolder}\{name}</c>.
        /// </summary>
        public TestSolution AddProject(string name)
        {
            var projectId = ProjectId.CreateNewId(name);

            var projectInfo = ProjectInfo
                .Create(
                    projectId,
                    VersionStamp.Create(),
                    name,
                    name,
                    LanguageNames.CSharp,
                    filePath: Path.Combine(SolutionFolder, name, name + ".csproj")
                    )
                .WithMetadataReferences(_metadataReferences)
                //a library, otherwise every compilation reports the missing entry point
                .WithCompilationOptions(
                    new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary
                        )
                    )
                ;

            Apply(Workspace.CurrentSolution.AddProject(projectInfo));

            _projects[name] = projectId;

            return this;
        }

        /// <summary>
        /// Make <paramref name="projectName"/> reference <paramref name="referencedProjectName"/>.
        /// </summary>
        public TestSolution AddProjectReference(string projectName, string referencedProjectName)
        {
            Apply(
                Workspace.CurrentSolution.AddProjectReference(
                    _projects[projectName],
                    new Microsoft.CodeAnalysis.ProjectReference(_projects[referencedProjectName])
                    )
                );

            return this;
        }

        /// <summary>
        /// Add a C# document into the project.
        /// </summary>
        /// <param name="projectName">Name of the project (it has to be added already).</param>
        /// <param name="relativeFilePath">Path of the file relative to the project folder.</param>
        /// <param name="body">Content of the file.</param>
        public TestSolution AddDocument(string projectName, string relativeFilePath, string body)
        {
            var projectId = _projects[projectName];
            var documentId = DocumentId.CreateNewId(projectId);
            var filePath = PathOf(projectName, relativeFilePath);

            Apply(
                Workspace.CurrentSolution.AddDocument(
                    documentId,
                    Path.GetFileName(filePath),
                    SourceText.From(body),
                    filePath: filePath
                    )
                );

            _documents[filePath] = documentId;

            return this;
        }

        /// <summary>
        /// Create a real xaml file inside the project folder.
        /// The xaml subsystem reads and writes the files directly, so this one is not
        /// a part of the workspace.
        /// </summary>
        /// <returns>Full path of the created file.</returns>
        public string AddXamlFile(string projectName, string relativeFilePath, string body)
        {
            return AddXamlFile(projectName, relativeFilePath, body, new UTF8Encoding(false));
        }

        /// <summary>
        /// Create a real xaml file in the given encoding. Visual Studio writes the xaml files
        /// as UTF-8 with a byte order mark, so the encoding matters here.
        /// </summary>
        /// <returns>Full path of the created file.</returns>
        public string AddXamlFile(string projectName, string relativeFilePath, string body, Encoding encoding)
        {
            var filePath = PathOf(projectName, relativeFilePath);

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, body, encoding);

            return filePath;
        }

        /// <summary>
        /// The current content of the xaml file on the disk, as raw bytes.
        /// </summary>
        public byte[] XamlBytesOf(string projectName, string relativeFilePath)
        {
            return File.ReadAllBytes(PathOf(projectName, relativeFilePath));
        }

        /// <summary>
        /// Full path of a file of the project (the file itself may not exist).
        /// </summary>
        public string PathOf(string projectName, string relativeFilePath)
        {
            return Path.Combine(SolutionFolder, projectName, relativeFilePath);
        }

        /// <summary>
        /// The current text of the document, as it is in the workspace right now.
        /// </summary>
        public string TextOf(string projectName, string relativeFilePath)
        {
            var documentId = _documents[PathOf(projectName, relativeFilePath)];

            return Workspace.CurrentSolution
                .GetDocument(documentId)!
                .GetTextAsync()
                .Result
                .ToString()
                ;
        }

        /// <summary>
        /// The current content of the xaml file on the disk.
        /// </summary>
        public string XamlTextOf(string projectName, string relativeFilePath)
        {
            return File.ReadAllText(PathOf(projectName, relativeFilePath));
        }

        /// <summary>
        /// The symbol of a type of the solution.
        /// </summary>
        /// <param name="projectName">Project which declares the type.</param>
        /// <param name="typeFullName">Full name of the type, e.g. <c>A.B.Class1</c>.</param>
        /// <exception cref="InvalidOperationException">There is no such type in that project.</exception>
        public async System.Threading.Tasks.Task<INamedTypeSymbol> GetTypeAsync(
            string projectName,
            string typeFullName
            )
        {
            var project = Workspace.CurrentSolution.GetProject(_projects[projectName])!;

            var compilation = await project.GetCompilationAsync();
            var symbol = compilation!.GetTypeByMetadataName(typeFullName);

            if (symbol == null)
            {
                throw new InvalidOperationException($"There is no type '{typeFullName}' in the project '{projectName}'");
            }

            return symbol;
        }

        /// <summary>
        /// The compilation errors of the whole solution.
        ///
        /// The adjusting produces a text, and a text may look plausible and still not compile
        /// (an ambiguous name, a lost using clause), so the interesting tests ask the compiler
        /// instead of comparing the strings only.
        /// </summary>
        public async System.Threading.Tasks.Task<List<string>> CompilationErrorsAsync()
        {
            var result = new List<string>();

            foreach (var projectId in _projects.Values)
            {
                var project = Workspace.CurrentSolution.GetProject(projectId)!;

                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                {
                    continue;
                }

                foreach (var diagnostic in compilation.GetDiagnostics())
                {
                    if (diagnostic.Severity != DiagnosticSeverity.Error)
                    {
                        continue;
                    }

                    result.Add($"{project.Name}: {diagnostic}");
                }
            }

            return result;
        }

        /// <summary>
        /// All the C# file paths of the solution.
        /// </summary>
        public List<string> AllDocumentPaths()
        {
            return _documents.Keys.ToList();
        }

        public void Dispose()
        {
            Workspace.Dispose();

            try
            {
                Directory.Delete(SolutionFolder, true);
            }
            catch
            {
                //a leftover in the temp folder is not a reason to fail a test
            }
        }

        private void Apply(Microsoft.CodeAnalysis.Solution solution)
        {
            if (!Workspace.TryApplyChanges(solution))
            {
                throw new InvalidOperationException("Can't apply the changes to the test workspace");
            }
        }
    }
}
