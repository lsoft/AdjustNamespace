using AdjustNamespace.Namespace;
using AdjustNamespace.Settings;
using AdjustNamespace.VisualStudio;
using AdjustNamespace.Xaml.BodyProvider;
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
    /// see <see cref="Context"/>.
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

        /// <summary>
        /// The MEF composition an <see cref="AdhocWorkspace"/> lives in.
        ///
        /// The default one of Roslyn scans everything which looks like a workspace assembly
        /// next to the test dll, and the output folder also contains the older
        /// <c>Microsoft.CodeAnalysis.*</c> assemblies of Microsoft.VisualStudio.LanguageServices
        /// (which has no release for the Roslyn we need, see the .csproj), so it fails to load
        /// the types of them. Only the two assemblies a C# workspace really needs are composed
        /// here, and both of them are the ones we reference.
        /// </summary>
        private static readonly Microsoft.CodeAnalysis.Host.HostServices _hostServices =
            Microsoft.CodeAnalysis.Host.Mef.MefHostServices.Create(
                new[]
                {
                    typeof(Workspace).Assembly,
                    typeof(Microsoft.CodeAnalysis.CSharp.Formatting.CSharpFormattingOptions).Assembly,
                }
                );

        private readonly Dictionary<string, ProjectId> _projects = new();

        /// <summary>
        /// The projects of the target frameworks of a multi target project, keyed by the name
        /// of that project. A name found here is replaced with all of them, so a test names
        /// the project of the solution and not the projects Roslyn creates of it,
        /// see <see cref="AddMultiTargetProject"/>.
        /// </summary>
        private readonly Dictionary<string, List<string>> _targetProjects = new();

        /// <summary>
        /// The documents of the solution, grouped by the file they are built of.
        /// A file of a shared project (.shproj) and a file of a multi target project
        /// (<c>net48;net8.0</c>) belong to more than one project at once, and Visual Studio
        /// creates a separate Roslyn document for every one of them,
        /// see <see cref="AddSharedDocument"/>.
        /// </summary>
        private readonly Dictionary<string, List<DocumentId>> _documents = new();

        /// <summary>
        /// The text every document had at the last <see cref="SyncLinkedDocuments"/>
        /// (or at the moment it has been added).
        /// </summary>
        private readonly Dictionary<DocumentId, string> _lastKnownTexts = new();

        /// <summary>
        /// The folder of every project of the solution, including the shared ones
        /// (which are no Roslyn projects at all) and the folder shared by the targets
        /// of a multi target project.
        /// </summary>
        private readonly Dictionary<string, string> _folders = new();

        /// <summary>
        /// The projects of this solution understand the <c>union</c> declarations,
        /// see <see cref="WithUnionSupport"/>.
        /// </summary>
        private bool _unionSupport;

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
        /// Everything an adjusting session over this solution works with, without any real
        /// Visual Studio behind: the workspace is the <see cref="AdhocWorkspace"/> of this
        /// solution and the two Visual Studio bound members are the fakes of this folder.
        ///
        /// A fresh instance is built on every access, so a test may change
        /// <see cref="Settings"/> (see <see cref="WithSkippedFolders"/>) before it asks for it.
        /// </summary>
        public AdjustContext Context => new AdjustContext(
            Workspace,
            new FakeSolutionExplorer(this),
            new TargetNamespaceResolver(
                Settings,
                new FakeProjectDefaultNamespaceProvider()
                ),
            new NullDocumentOpener(),
            new ClosedXamlBodyProviderFactory()
            );

        /// <summary>
        /// The project of every file of this solution, as the solution tree of Visual Studio
        /// would report it: the documents of the workspace plus the xaml files on the disk.
        ///
        /// A file which several projects compile is reported with the first of them, exactly
        /// as <c>SolutionHelper.GetAllProjectItemsAsync</c> does it. The projects of the target
        /// frameworks of a multi target project are one project of the solution, so the name
        /// of that project is reported and not the name Roslyn gives to every target.
        /// </summary>
        public Dictionary<string, ProjectRef> ProjectOfEveryFile()
        {
            var result = new Dictionary<string, ProjectRef>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in _documents)
            {
                var project = Workspace.CurrentSolution.GetProject(pair.Value[0].ProjectId)!;

                result[pair.Key] = new ProjectRef(
                    SolutionLevelNameOf(project.Name),
                    project.FilePath!
                    );
            }

            //the xaml files are real files inside a project folder and are no documents
            //of the workspace, see AddXamlFile
            if (Directory.Exists(SolutionFolder))
            {
                foreach (var xamlFilePath in Directory.GetFiles(SolutionFolder, "*.xaml", SearchOption.AllDirectories))
                {
                    var owner = TryFindProjectOfFolderOf(xamlFilePath);
                    if (owner.HasValue)
                    {
                        result[xamlFilePath] = owner.Value;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// The project of the solution the given Roslyn project belongs to:
        /// <c>MyApp (net48)</c> is a target framework of the project <c>MyApp</c>.
        /// </summary>
        private string SolutionLevelNameOf(string roslynProjectName)
        {
            foreach (var pair in _targetProjects)
            {
                if (pair.Value.Contains(roslynProjectName))
                {
                    return pair.Key;
                }
            }

            return roslynProjectName;
        }

        /// <summary>
        /// The project whose folder contains the given file. The innermost folder wins,
        /// so a project inside the folder of another one is resolved correctly.
        /// </summary>
        private ProjectRef? TryFindProjectOfFolderOf(string filePath)
        {
            ProjectRef? result = null;
            var bestLength = -1;

            foreach (var pair in _folders)
            {
                var folder = pair.Value + Path.DirectorySeparatorChar;

                if (!filePath.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (folder.Length <= bestLength)
                {
                    continue;
                }

                bestLength = folder.Length;
                result = new ProjectRef(pair.Key, ProjectFilePathOf(pair.Key));
            }

            return result;
        }

        /// <summary>
        /// Full path to the project file. A shared project (.shproj) is no Roslyn project
        /// at all, see <see cref="AddSharedProject"/>, so its path is built out of its name.
        /// </summary>
        private string ProjectFilePathOf(string projectName)
        {
            if (_projects.TryGetValue(projectName, out var projectId))
            {
                return Workspace.CurrentSolution.GetProject(projectId)!.FilePath!;
            }

            if (_targetProjects.TryGetValue(projectName, out var targetNames) && targetNames.Count > 0)
            {
                return Workspace.CurrentSolution.GetProject(_projects[targetNames[0]])!.FilePath!;
            }

            return Path.Combine(_folders[projectName], projectName + ".shproj");
        }

        public TestSolution()
        {
            SolutionFolder = Path.Combine(
                Path.GetTempPath(),
                "AdjustNamespace.Tests",
                Guid.NewGuid().ToString("N")
                );
            Directory.CreateDirectory(SolutionFolder);

            Workspace = new AdhocWorkspace(_hostServices);
            Settings = new AdjustNamespaceSettings2(
                SolutionFolder,
                new AdjustNamespaceSettings()
                );
        }

        /// <summary>
        /// Compile the projects of this solution with the preview features of the language
        /// and give them the runtime types a <c>union</c> declaration needs,
        /// see <see cref="UnionSupportBody"/>.
        ///
        /// It has to be called before the projects are added: the parse options of a project
        /// are a part of it.
        /// </summary>
        public TestSolution WithUnionSupport()
        {
            _unionSupport = true;

            return this;
        }

        /// <summary>
        /// The types a union declaration is lowered to. They are a part of the framework
        /// .NET 11 ships and do not exist in the reference assemblies of net48 this solution
        /// is compiled against, so they are declared as a source file of every project,
        /// exactly as a project which targets an older framework has to do it.
        ///
        /// <c>IsExternalInit</c> is here for the same reason: a case type of a union is
        /// usually a positional record.
        ///
        /// <c>System.*</c> is a special namespace for the extension
        /// (<c>NamespaceHelper.IsSpecialNamespace</c>), so this file is never adjusted.
        /// </summary>
        public const string UnionSupportBody =
@"namespace System.Runtime.CompilerServices
{
    public interface IUnion
    {
        object Value { get; }
    }

    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class UnionAttribute : Attribute
    {
    }

    public static class IsExternalInit
    {
    }
}
";

        /// <summary>
        /// The name of the file <see cref="WithUnionSupport"/> adds to every project.
        /// </summary>
        public const string UnionSupportFileName = "UnionSupport.cs";

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
            _folders[name] = Path.Combine(SolutionFolder, name);

            AddProject(name, Path.Combine(SolutionFolder, name, name + ".csproj"));

            AddUnionSupportTo(name);

            return this;
        }

        /// <summary>
        /// Give the project the runtime types a union declaration needs,
        /// if <see cref="WithUnionSupport"/> has been asked for.
        /// </summary>
        private void AddUnionSupportTo(string projectName)
        {
            if (!_unionSupport)
            {
                return;
            }

            AddDocument(projectName, UnionSupportFileName, UnionSupportBody);
        }

        /// <summary>
        /// Add a shared project (.shproj). Such a project is no Roslyn project: its files
        /// are compiled by every project which references it, so there is nothing to add
        /// to the workspace here and only the folder of it is registered.
        /// Its documents are added with <see cref="AddSharedDocument"/>.
        /// </summary>
        public TestSolution AddSharedProject(string name)
        {
            _folders[name] = Path.Combine(SolutionFolder, name);

            return this;
        }

        /// <summary>
        /// Add a multi target project (<c>&lt;TargetFrameworks&gt;net48;net8.0&lt;/TargetFrameworks&gt;</c>).
        /// Visual Studio creates a separate Roslyn project for every target framework of such
        /// a project, and all of them share the very same project file and folder.
        /// The projects are named <c>{name} ({targetFramework})</c> and every one of them
        /// defines the conditional compilation symbol of its target framework,
        /// see <see cref="PreprocessorSymbolOf"/>.
        ///
        /// Everything which takes a project name (<see cref="AddDocument"/>,
        /// <see cref="AddProjectReference"/>, <see cref="AddSharedDocument"/>) accepts the name
        /// of such a project and means all of its target frameworks;
        /// <see cref="AddMultiTargetDocument"/> adds a file of a chosen part of them
        /// (<c>&lt;Compile Condition="'$(TargetFramework)'=='net48'" /&gt;</c>).
        /// </summary>
        public TestSolution AddMultiTargetProject(string name, params string[] targetFrameworks)
        {
            _folders[name] = Path.Combine(SolutionFolder, name);
            _targetProjects[name] = new List<string>();

            foreach (var targetFramework in targetFrameworks)
            {
                var targetName = TargetProjectName(name, targetFramework);

                _folders[targetName] = _folders[name];
                _targetProjects[name].Add(targetName);

                AddProject(
                    targetName,
                    Path.Combine(SolutionFolder, name, name + ".csproj"),
                    new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(
                        preprocessorSymbols: new[] { PreprocessorSymbolOf(targetFramework) }
                        )
                    );
            }

            AddUnionSupportTo(name);

            return this;
        }

        /// <summary>
        /// The name of the Roslyn project which compiles the given target framework
        /// of a multi target project.
        /// </summary>
        public static string TargetProjectName(string projectName, string targetFramework)
        {
            return $"{projectName} ({targetFramework})";
        }

        /// <summary>
        /// The conditional compilation symbol of a target framework
        /// (<c>net48</c> -> <c>NET48</c>, <c>net8.0</c> -> <c>NET8_0</c>).
        /// The additional symbols of a real build (<c>NETFRAMEWORK</c>, <c>NET</c>,
        /// <c>NET8_0_OR_GREATER</c>) are not defined here.
        /// </summary>
        public static string PreprocessorSymbolOf(string targetFramework)
        {
            return targetFramework.ToUpperInvariant().Replace('.', '_').Replace('-', '_');
        }

        private void AddProject(
            string name,
            string projectFilePath,
            Microsoft.CodeAnalysis.CSharp.CSharpParseOptions? parseOptions = null
            )
        {
            var projectId = ProjectId.CreateNewId(name);

            if (_unionSupport)
            {
                //the unions are a preview feature of the language, see WithUnionSupport
                parseOptions = (parseOptions ?? new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions())
                    .WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview);
            }

            var projectInfo = ProjectInfo
                .Create(
                    projectId,
                    VersionStamp.Create(),
                    name,
                    name,
                    LanguageNames.CSharp,
                    filePath: projectFilePath
                    )
                .WithMetadataReferences(_metadataReferences)
                //a library, otherwise every compilation reports the missing entry point
                .WithCompilationOptions(
                    new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                        OutputKind.DynamicallyLinkedLibrary
                        )
                    )
                ;

            if (parseOptions != null)
            {
                projectInfo = projectInfo.WithParseOptions(parseOptions);
            }

            Apply(Workspace.CurrentSolution.AddProject(projectInfo));

            _projects[name] = projectId;
        }

        /// <summary>
        /// Make <paramref name="projectName"/> reference <paramref name="referencedProjectName"/>.
        /// Every target framework of a multi target project references it.
        /// </summary>
        public TestSolution AddProjectReference(string projectName, string referencedProjectName)
        {
            foreach (var name in ExpandTargets(projectName))
            {
                Apply(
                    Workspace.CurrentSolution.AddProjectReference(
                        _projects[name],
                        new Microsoft.CodeAnalysis.ProjectReference(_projects[referencedProjectName])
                        )
                    );
            }

            return this;
        }

        /// <summary>
        /// The projects which compile the files of the named project: the project itself,
        /// or the projects of all the target frameworks of a multi target project.
        /// </summary>
        private IEnumerable<string> ExpandTargets(string projectName)
        {
            if (_targetProjects.TryGetValue(projectName, out var targetNames))
            {
                return targetNames;
            }

            return new[] { projectName };
        }

        /// <summary>
        /// Add a C# document into the project.
        /// </summary>
        /// <param name="projectName">Name of the project (it has to be added already).</param>
        /// <param name="relativeFilePath">Path of the file relative to the project folder.</param>
        /// <param name="body">Content of the file.</param>
        public TestSolution AddDocument(string projectName, string relativeFilePath, string body)
        {
            AddDocument(PathOf(projectName, relativeFilePath), body, new[] { projectName });

            return this;
        }

        /// <summary>
        /// Add a file of a shared project into every project which references that shared
        /// project. There is a single file on the disk and one Roslyn document per referencing
        /// project, exactly as Visual Studio builds it.
        /// </summary>
        /// <param name="sharedProjectName">Name of the shared project (it has to be added already).</param>
        /// <param name="relativeFilePath">Path of the file relative to the shared project folder.</param>
        /// <param name="body">Content of the file.</param>
        /// <param name="projectNames">The projects which reference the shared project.</param>
        public TestSolution AddSharedDocument(
            string sharedProjectName,
            string relativeFilePath,
            string body,
            params string[] projectNames
            )
        {
            AddDocument(PathOf(sharedProjectName, relativeFilePath), body, projectNames);

            return this;
        }

        /// <summary>
        /// Add a file of a multi target project. Every target framework of such a project
        /// compiles it, so it becomes a document of every project
        /// <see cref="AddMultiTargetProject"/> has created.
        /// </summary>
        public TestSolution AddMultiTargetDocument(
            string projectName,
            string relativeFilePath,
            string body,
            params string[] targetFrameworks
            )
        {
            AddDocument(
                PathOf(projectName, relativeFilePath),
                body,
                Array.ConvertAll(targetFrameworks, tf => TargetProjectName(projectName, tf))
                );

            return this;
        }

        /// <summary>
        /// Replace the content of a document which has been added already.
        ///
        /// This is how a generated file behaves: it is not touched by the adjusting itself
        /// and is rewritten by the next build out of the sources the adjusting has changed,
        /// so a test is able to state what the solution looks like after that build.
        /// </summary>
        /// <param name="projectName">Name of the project the document belongs to.</param>
        /// <param name="relativeFilePath">Path of the file relative to the project folder.</param>
        /// <param name="body">The new content of the file.</param>
        public TestSolution ReplaceDocument(string projectName, string relativeFilePath, string body)
        {
            foreach (var documentId in _documents[PathOf(projectName, relativeFilePath)])
            {
                Apply(
                    Workspace.CurrentSolution.WithDocumentText(
                        documentId,
                        SourceText.From(body)
                        )
                    );

                _lastKnownTexts[documentId] = body;
            }

            return this;
        }

        private void AddDocument(string filePath, string body, string[] projectNames)
        {
            foreach (var projectName in projectNames.SelectMany(ExpandTargets))
            {
                var projectId = _projects[projectName];
                var documentId = DocumentId.CreateNewId(projectId);

                Apply(
                    Workspace.CurrentSolution.AddDocument(
                        documentId,
                        Path.GetFileName(filePath),
                        SourceText.From(body),
                        filePath: filePath
                        )
                    );

                if (!_documents.TryGetValue(filePath, out var documentIds))
                {
                    documentIds = new List<DocumentId>();
                    _documents[filePath] = documentIds;
                }

                documentIds.Add(documentId);
                _lastKnownTexts[documentId] = body;
            }
        }

        /// <summary>
        /// Propagate the changes of a file which is compiled by several projects to every
        /// document of that file.
        ///
        /// The extension changes such a file through a single one of its documents
        /// (see <c>WorkspaceExtensions.GetDocument</c>), and in Visual Studio that is enough:
        /// there is one file on the disk and one text buffer for all of the documents.
        /// The <see cref="AdhocWorkspace"/> knows nothing about it, hence this explicit step.
        /// It is performed by <see cref="TextOf"/> and <see cref="CompilationErrorsAsync"/>,
        /// so a test does not have to care about it.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The documents of one file have been changed differently. This is a defect of the
        /// extension: a file may not be adjusted twice, once per project which compiles it.
        /// </exception>
        public void SyncLinkedDocuments()
        {
            foreach (var pair in _documents)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                var changedTexts = pair.Value
                    .ConvertAll(TextOf)
                    .FindAll(t => t != _lastKnownTexts[pair.Value[0]])
                    .Distinct()
                    .ToList();

                if (changedTexts.Count == 0)
                {
                    continue;
                }

                if (changedTexts.Count > 1)
                {
                    throw new InvalidOperationException(
                        $"The documents of '{pair.Key}' have been changed differently:{Environment.NewLine}"
                        + string.Join(Environment.NewLine + "---" + Environment.NewLine, changedTexts)
                        );
                }

                var text = changedTexts[0];

                foreach (var documentId in pair.Value)
                {
                    if (TextOf(documentId) != text)
                    {
                        Apply(
                            Workspace.CurrentSolution.WithDocumentText(
                                documentId,
                                SourceText.From(text)
                                )
                            );
                    }

                    _lastKnownTexts[documentId] = text;
                }
            }
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
            if (!_folders.TryGetValue(projectName, out var folder))
            {
                folder = Path.Combine(SolutionFolder, projectName);
            }

            return Path.Combine(folder, relativeFilePath);
        }

        /// <summary>
        /// The current text of the document, as it is in the workspace right now.
        /// A file which is compiled by several projects has a single text,
        /// see <see cref="SyncLinkedDocuments"/>.
        /// </summary>
        public string TextOf(string projectName, string relativeFilePath)
        {
            SyncLinkedDocuments();

            return TextOf(_documents[PathOf(projectName, relativeFilePath)][0]);
        }

        private string TextOf(DocumentId documentId)
        {
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
            SyncLinkedDocuments();

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
        /// The unresolved <c>cref</c> references of the whole solution
        /// (<c>&lt;see cref="Class1"/&gt;</c> which does not point to anything anymore).
        ///
        /// Such a reference is a warning (CS1574) and not an error, and only when the project
        /// generates the documentation file: the parse options of a project decide whether the
        /// compiler binds the crefs at all, and the default one does not
        /// (<see cref="DocumentationMode.Parse"/>). The projects are therefore re-parsed here
        /// with <see cref="DocumentationMode.Diagnose"/>, on a copy of the solution which is
        /// not applied to the workspace, so this check costs nothing to the tests which do not
        /// ask for it.
        /// </summary>
        public async System.Threading.Tasks.Task<List<string>> UnresolvedCrefsAsync()
        {
            SyncLinkedDocuments();

            var solution = Workspace.CurrentSolution;

            foreach (var projectId in _projects.Values)
            {
                var parseOptions =
                    (Microsoft.CodeAnalysis.CSharp.CSharpParseOptions)
                    (solution.GetProject(projectId)!.ParseOptions
                        ?? new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions());

                solution = solution.WithProjectParseOptions(
                    projectId,
                    parseOptions.WithDocumentationMode(DocumentationMode.Diagnose)
                    );
            }

            var result = new List<string>();

            foreach (var projectId in _projects.Values)
            {
                var project = solution.GetProject(projectId)!;

                var compilation = await project.GetCompilationAsync();
                if (compilation == null)
                {
                    continue;
                }

                foreach (var diagnostic in compilation.GetDiagnostics())
                {
                    //CS1574: the cref points to nothing, CS1580/CS1581/CS1584: it points
                    //to a member which does not exist or is written incorrectly
                    if (diagnostic.Id.NotIn("CS1574", "CS1580", "CS1581", "CS1584"))
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
