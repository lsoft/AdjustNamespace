# Tests

The extension is covered on two levels:

- **`AdjustNamespace.Tests`** — the automated tests of the core, see below;
- **the sample solution** (`Standard` / `Subject`) — the manual end to end run inside a real
  Visual Studio, see [Manual tests](#manual-tests).

# Automated tests

`AdjustNamespace.Tests` is an ordinary SDK style project which imports the shared project
`AdjustNamespace.VsixShared`, so it works with the very same code as the VSIX. It is built and
run with the plain `dotnet` CLI, the full MSBuild and the VSSDK are not required here:

```bash
dotnet test Tests/AdjustNamespace.Tests/AdjustNamespace.Tests.csproj
```

The tests run without Visual Studio at all:

- the xaml subsystem works with a plain string through `MemoryXamlBodyProvider`;
- the core (`CsAdjuster`, the fixers, `Cleanup`) works over an `AdhocWorkspace` built by
  `TestSolution`, and `VsServices.CreateForTests` binds it into a `VsServices` without any
  Visual Studio service behind. Everything which needs DTE, the solution tree or the editor
  (`SubjectFileCollector`, `NamespaceHelper.TryDetermineTargetNamespaceAsync`, the wizard) is
  still covered by the manual test only.

Everything is green, see [known bugs](#known-bugs). What is covered:

| Area | Tests |
| --- | --- |
| The whole pipeline over a solution | `Adjusting\CsAdjusterTests`, `CsAdjusterReferenceTests` (every kind of a reference: qualified names, `global::`, generics, base types, type constraints, attributes, static members, alias and static usings, xaml), `CsAdjusterSessionTests` (several files in one session, the cleanup, the repeated runs) |
| The partially qualified names | `Adjusting\CsAdjusterPartialNameTests` (`B.Class1` resolved through the own namespace or through an alias, `typeof`/`nameof`) |
| The member access expressions | `Adjusting\CsAdjusterMemberAccessTests` (a static member of a generic, of a nested and of a static class) |
| The using clauses | `Adjusting\UsingPlacementTests` (a file without any using, a header, a region, a `global using`), `Adjusting\CleanupTests` (when an old using has to disappear and when it must not) |
| The kinds of the declarations | `Adjusting\CsAdjusterTypeKindTests` (a record, a struct, a static and a generic class, the contradicting namespace declarations) |
| The xaml files | `Xaml\XamlDocumentTests` (parsing and moving), `XamlReferenceKindTests` (the references outside of a tag and of a markup extension), `XamlFileWritingTests` (the encoding and the line endings of the written file), `Adjusting\XamlAdjusterTests` (the `x:Class` of the document itself) |
| The shared projects | `Adjusting\SharedProjectTests` (one file compiled by several projects: the ambiguous target namespace of a C# and of a xaml file, the references and the using clauses of every project, the file list of the solution, a namespace which several projects fill) |
| The multi target projects | `Adjusting\MultiTargetTests` (one file compiled by every target framework: the references and the using clauses, a file of a single target framework, xaml, the conditional compilation, a shared project referenced by a multi target one) |
| The namespaces | `Namespace\NamespaceTransitionContainerTests`, `NamespaceNodeSearchTests`, `NamespaceCenterTests`, `NamespaceHelperTests` |
| The fixers | `Fixer\AddUsingFixerTests` |
| The helpers | `Helper\RoslynHelperTests`, `PredicateTests` |
| The settings | `Settings\SkippedFolderTests`, `NamespaceReplaceRegexTests` |
| The name conflicts | `TypeContainerTests` |

A test may ask the compiler instead of comparing the strings only:
`TestSolution.CompilationErrorsAsync()` returns the errors of the whole solution, so
`Assert.Empty(await solution.CompilationErrorsAsync())` before and after the adjusting states
that the result is not only plausible but also buildable. This is the only way to catch the
cases where the produced name is a valid one but resolves to another type.

## Known bugs

A test which reveals an error before that error is fixed gets the trait `KnownBug`: it describes
the behaviour we *want* to have and is red on purpose. Such a test must not be "fixed" to make it
green — either fix the error in the extension and remove the trait, or leave the test as it is.

While such tests exist, a clean run is

```bash
dotnet test Tests/AdjustNamespace.Tests/AdjustNamespace.Tests.csproj --filter "Category!=KnownBug"
```

and the failures of the full run are the list of the errors to fix.

**Right now there is no such test: the whole suite is green.** The errors which have been
described here and are fixed since then:

| The error | How it is fixed |
| --- | --- |
| A rewritten name is a relative one: `X.Y.Class1` written inside the namespace `Some.X` resolves to `Some.X.Y.Class1` and does not compile. | `RefProcessor.IsGlobalPrefixRequired` asks the semantic model whether the first part of the target namespace is shadowed at that position and prefixes the name with `global::` if it is. |
| A new using clause is added behind the last using of the file, and the using clauses inside a namespace declaration are visible in that namespace only: a file with several namespaces gets the clause into the wrong one. | `AddUsingFixer` looks at the using clauses of the compilation unit only and writes the new one among them: such a clause is visible in every namespace of the file and its name is resolved from the root namespace. |
| `namespace A { namespace B { } }` plus `namespace A.B { }` in one file produce two different transitions of `A.B`, and the references are fixed with the wrong one of them. | A type is moved by the transition of the declaration it is written in (`NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf`) and not by a lookup of its namespace name. |
| A xaml class is referenced not only by a tag and by an `{x:Type}`/`{x:Static}` markup extension, but also by an attribute value (`TargetType="local:MyButton"`), by an attached property, by a custom markup extension and by `x:TypeArguments`. Such a reference is not moved and keeps pointing to the old namespace. | Everything which looks like an `alias:ClassName` pair and is not a part of an already recognized fragment becomes a `XamlTypeUsage`. A pair is rewritten only if its alias is a clr-namespace one which points to the namespace the class is moved out of, so the pairs which are no type references at all (`mc:Ignorable="d"`) cost nothing. |

### A note about the shared projects

A file which more than one project compiles is left as it is: whatever namespace is chosen for
it, it is derived from one of these projects and does not match the other ones. Two cases which
look the same in Roslyn (one file, several documents) are kept apart by
`WorkspaceHelper.IsCompiledBySeveralProjects`:

- a **shared project** referenced by several projects — the file belongs to several projects
  with **different project files**, there is no target namespace and the file is skipped;
- a **multi target project** (`net48;net8.0`) — Roslyn creates a project per target framework,
  so the file belongs to several projects with the **same project file**. Such a file is
  adjusted as usual, see `A_file_of_a_multi_target_project_is_adjusted`.

A shared project referenced by a single project is not ambiguous either and is adjusted, see
`A_file_of_a_shared_project_of_a_single_project_is_adjusted`. The fallback of
`NamespaceHelper.GetProjectDefaultNamespaceAsync` (the default namespace of a project of an
unknown kind is its name without the last part, `MyApp.Shared` -> `MyApp`) serves exactly that
case now.

### A note about the multi target projects

The projects of the target frameworks of a multi target project are not copies of each other:
every one of them defines its own conditional compilation symbols and may compile its own files
(`<Compile Condition="'$(TargetFramework)'=='net48'" />`). A file of such a project has one text
and a syntax tree per target framework, so everything which reads it reads all of its documents
(`WorkspaceHelper.GetDocuments`) and everything which writes it addresses a span of the text and
not a node of a tree — a name which is a name for one target framework is a part of a disabled
text for another one.

Two of the tests describe the case which cannot be made consistent that way: the target
frameworks disagree whether the namespace the file is moved out of stays alive, and the `using`
clause of it is required by one of them and does not compile for another one. Such a file is
left as it is, exactly as a file of a shared project which several projects compile.

A file which belongs to a single target framework is an ordinary file for the extension and is
adjusted as usual (`A_file_of_a_single_target_framework_is_adjusted`).

The combination of both kinds is covered as well: a shared project referenced by a single multi
target project is adjusted (one project of the solution), and a shared project referenced by a
multi target project plus anything else is not.

## Adding a test

The files of `AdjustNamespace.Tests` are picked up by a glob, no need to register them anywhere
(unlike the shared project, see [../CLAUDE.md](../CLAUDE.md)). Keep the repro as small as
possible and mention the issue number if the case is related to a github issue.

A test which needs several projects builds them with `TestSolution`:

```csharp
using var solution = new TestSolution()
    .AddProject("MyApp")
    .AddProject("MyApp.Consumers")
    .AddProjectReference("MyApp.Consumers", "MyApp")
    .AddDocument("MyApp", "Class1.cs", "namespace A.B { public class Class1 { } }")
    .AddDocument("MyApp.Consumers", "Consumer.cs", "...")
    ;
```

Every project gets its own folder `{SolutionFolder}\{name}` and its own `{name}.csproj` path, so
the target namespaces and the skipped folders behave as they do in a real solution. There are no
`.csproj` files on the disk and no MSBuild behind: the projects, their references and their
documents live in an `AdhocWorkspace`, exactly as Roslyn sees them inside Visual Studio.

A file which is compiled by more than one project (a shared project or a multi target one) is
added with `AddSharedDocument` / `AddMultiTargetDocument`:

```csharp
using var solution = new TestSolution()
    .AddProject("A")
    .AddProject("B")
    .AddSharedProject("Common")
    .AddSharedDocument("Common", "Class1.cs", "namespace Legacy.Core { public class Class1 { } }", "A", "B")
    ;
```

A multi target project is added with `AddMultiTargetProject`:

```csharp
using var solution = new TestSolution()
    .AddMultiTargetProject("MyApp", "net48", "net8.0")
    //a file of all the target frameworks
    .AddDocument("MyApp", "Class1.cs", "namespace A.B { public class Class1 { } }")
    //a file of a single one of them
    .AddMultiTargetDocument("MyApp", "Legacy.cs", "namespace A.B { public class Legacy { } }", "net48")
    ;
```

It creates a Roslyn project per target framework (`MyApp (net48)`, `MyApp (net8.0)`), all of them
with the same project file, and every one of them defines the conditional compilation symbol of
its target framework (`NET48`, `NET8_0`; the additional symbols of a real build, `NETFRAMEWORK`
and `NET8_0_OR_GREATER`, are not defined). Everything which takes a project name accepts the name
of such a project and means all of its target frameworks.

A shared project is no Roslyn project at all: only the folder of it is registered, and its file
becomes a document of every project which references it, exactly as Visual Studio builds it.
Visual Studio keeps a single file on the disk for all of these documents, so a change of any one
of them is a change of all of them; the `AdhocWorkspace` knows nothing about it and
`TestSolution` propagates such a change itself (`SyncLinkedDocuments`, performed by `TextOf` and
`CompilationErrorsAsync`, so a test does not have to care). If the extension changes the
documents of one file *differently*, the propagation throws instead of hiding it.

# Manual tests

The extension modifies the code of a solution opened in Visual Studio, so the whole wizard is
tested manually against a sample solution which lives here.

## Folders

| Folder | Contents |
| --- | --- |
| `Standard` | The pristine sample solution. It is under the source control, do not adjust it. |
| `Subject` | A working copy of `Standard`. It is recreated by the post-build event of `AdjustNamespace.2022` and is ignored by git. |
| `Result` | A place for the expected results, if you want to keep them for a comparison. |

## The sample solution

`Subject\TestSolution.sln` contains the cases which have already been broken at least once:

| Project | What it covers |
| --- | --- |
| `TestProject` | Plain C# and WPF: classic, nested and file scoped namespaces, several namespaces in a single file, types declared in a `System.*` namespace (they must not be touched), nested and generic types, type constraints, WPF user controls with `x:Type` / `x:Static` references. |
| `TestSharedProject` | A shared project (`.shproj`): the default namespace is derived from the project name. It is imported by `TestProject` only, so the ambiguous case (a shared project referenced by several projects, see `SharedProjectTests`) is not covered here yet. |
| `DatabaseProject` | C# files inside a `sqlproj`. |
| `TestMauiApp` | MAUI xaml, which uses another uri for the xaml language namespace. |

`Subject\adjust_namespaces_settings.xml` contains the samples of the skipped folders (both a
rooted path and a path relative to the solution folder).

## How to run a test

1. Build `AdjustNamespace.2022`. The post-build event recreates `Tests\Subject` from
   `Tests\Standard`, so every run starts from the same state.
2. Press F5 to start the experimental instance of Visual Studio.
3. Open `Tests\Subject\TestSolution.sln` there and build it: the extension relies on the
   semantic model, and the first wizard step reports the compilation errors.
4. Run one of the commands of the extension and follow the wizard.
5. Check the results: the adjusted solution has to be compiled successfully, and the namespaces
   of the adjusted files have to match their folders (except the folders excluded in the
   settings file).

`git status` is useless inside `Tests\Subject` (the folder is ignored), so compare it with
`Tests\Standard` if you need to review what exactly has been changed.

## Adding a new case

Add the repro into the corresponding project of `Tests\Standard` (or create a new project there),
rebuild `AdjustNamespace.2022` and check that the case is processed correctly. Please keep the
repro as small as possible and mention the issue number in the file or folder name if it is
related to a github issue.
