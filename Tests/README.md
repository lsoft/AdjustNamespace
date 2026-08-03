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

Everything except the [known bugs](#known-bugs) is green. What is covered:

| Area | Tests |
| --- | --- |
| The whole pipeline over a solution | `Adjusting\CsAdjusterTests`, `CsAdjusterReferenceTests` (every kind of a reference: qualified names, `global::`, generics, base types, type constraints, attributes, static members, alias and static usings, xaml), `CsAdjusterSessionTests` (several files in one session, the cleanup, the repeated runs) |
| The partially qualified names | `Adjusting\CsAdjusterPartialNameTests` (`B.Class1` resolved through the own namespace or through an alias, `typeof`/`nameof`) |
| The member access expressions | `Adjusting\CsAdjusterMemberAccessTests` (a static member of a generic, of a nested and of a static class) |
| The using clauses | `Adjusting\UsingPlacementTests` (a file without any using, a header, a region, a `global using`), `Adjusting\CleanupTests` (when an old using has to disappear and when it must not) |
| The kinds of the declarations | `Adjusting\CsAdjusterTypeKindTests` (a record, a struct, a static and a generic class, the contradicting namespace declarations) |
| The xaml files | `Xaml\XamlDocumentTests` (parsing and moving), `XamlReferenceKindTests` (the references outside of a tag and of a markup extension), `XamlFileWritingTests` (the encoding and the line endings of the written file), `Adjusting\XamlAdjusterTests` (the `x:Class` of the document itself) |
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

and the failures of the full run are the list of the errors to fix. Right now these are:

| Test | The error it describes |
| --- | --- |
| `CsAdjusterPartialNameTests.A_rewritten_name_is_resolved_unambiguously` | A rewritten name is a relative one: `X.Y.Class1` written inside the namespace `Some.X` resolves to `Some.X.Y.Class1` and does not compile. Only a `global::` prefix makes such a name unambiguous. |
| `UsingPlacementTests.The_new_using_is_added_into_the_namespace_which_needs_it` | A new using clause is added behind the last using of the file, and the using clauses inside a namespace declaration are visible in that namespace only: a file with several namespaces gets the clause into the wrong one. |
| `CsAdjusterTypeKindTests.The_contradicting_declarations_of_one_namespace_are_processed` | `namespace A { namespace B { } }` plus `namespace A.B { }` in one file produce two different transitions of `A.B`, and the references are fixed with the wrong one of them. |
| `XamlReferenceKindTests.A_class_referenced_by_a_bare_attribute_value_is_moved` and three more of that file | A xaml class is referenced not only by a tag and by an `{x:Type}`/`{x:Static}` markup extension, but also by an attribute value (`TargetType="local:MyButton"`), by an attached property, by a custom markup extension and by `x:TypeArguments`. Such a reference is not moved and keeps pointing to the old namespace. |

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
| `TestSharedProject` | A shared project (`.shproj`): the default namespace is derived from the project name. |
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
