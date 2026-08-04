# Architecture

This document describes how AdjustNamespace is arranged inside. It is intended for the
contributors; if you are looking for the user documentation, please read [../README.md](../README.md).

## Projects

| Project | Contents |
| --- | --- |
| `AdjustNamespace.VsixShared` | A shared MSBuild project with the whole code of the extension. |
| `AdjustNamespace.2022` | The VSIX project for Visual Studio 2022: the manifest, the command table (`VSCommandTable.vsct`), the image manifest and the resources. |

The extension targets .NET Framework 4.8 and is built against
[Community.VisualStudio.Toolkit](https://github.com/VsixCommunity/Community.VisualStudio.Toolkit),
Roslyn (`Microsoft.CodeAnalysis.*`) and `Microsoft.VisualStudio.LanguageServices`.
The code which depends on the Visual Studio version is guarded with the `VS2022`
conditional compilation symbol (file scoped namespaces, for example).

The post-build event of `AdjustNamespace.2022` refreshes the `Tests/Subject` folder from
`Tests/Standard`, see [../Tests/README.md](../Tests/README.md).

## The big picture

```
 Command (AdjustNamespaceCommand / AdjustSolutionCommand / AdjustSelectedCommand)
   |  collects the file paths chosen by the user
   v
 AdjustNamespaceWindow (modal wizard)
   |
   +--> 1. PreparationStepViewModel   - compiles the solution, reports the errors
   |
   +--> 2. SelectedStepViewModel      - SubjectFileCollector scans the files,
   |                                    the user chooses what to adjust
   |
   +--> 3. PerformingViewModel        - AdjusterFactory + IAdjuster do the job,
                                        Cleanup removes the emptied usings
```

### Namespaces of the codebase

| Namespace | Responsibility |
| --- | --- |
| `AdjustNamespace.Command` | Menu commands. Each of them collects the file paths and opens the wizard. |
| `AdjustNamespace.UI` | The wizard: the window, the steps (`StepFactory`), the viewmodels and the WPF controls. |
| `AdjustNamespace.Adjusting` | The core: the scanner, the adjusters, the fixers and the final cleanup. |
| `AdjustNamespace.Namespace` | Namespace transitions and the namespace state of the solution. |
| `AdjustNamespace.Xaml` | Reading, modification and saving of the xaml files. |
| `AdjustNamespace.Settings` | Per solution settings stored in the solution folder. |
| `AdjustNamespace.Options` | Per user options stored by Visual Studio. |
| `AdjustNamespace.InfoBar` | The release notes gold bar. |
| `AdjustNamespace.Helper` | Helpers around Roslyn, the solution tree, WPF and MVVM. |

`VsServices` is a small struct which carries the Visual Studio services (DTE, the Roslyn
workspace, the component model) plus the settings of the opened solution. It is created once
per command invocation and is passed everywhere by value.

## The wizard

The steps are chained through `IStepFactory`: every step knows the factory of the next one and
replaces the content of the wizard window with the control of that step. The parameters are
passed as a plain `object` (`SelectedStepParameters`, `PerformingParameters`).

1. **`PreparationStepViewModel`** compiles every project of the solution and shows the found
   errors. The adjusting relies on the semantic model, so a broken solution may produce
   incorrect results. The user is allowed to move next anyway.
2. **`SelectedStepViewModel`** runs `SubjectFileCollector` and shows the files which are really
   the subject to change, grouped by their physical folder (`SelectFolderViewModel` +
   `SelectFileViewModel`). Here the user tunes the target namespace regex
   (`NamespaceReplaceRegex`, `KnownRegex`) and decides whether the affected files have to be
   opened in the editor (this is the only way to make the changes undoable).
3. **`PerformingViewModel`** creates `NamespaceCenter` and `AdjusterFactory`, adjusts the chosen
   files one by one and finally runs `Cleanup` over every C# document of the solution.

## The core

### Determining the target namespace

`NamespaceHelper.TryDetermineTargetNamespaceAsync` builds the target namespace as
`project default namespace` + `folders between the project folder and the file`, skips the
folders excluded by the user (`AdjustNamespaceSettings2.IsSkippedFolder`) and applies the user
regex. The default namespace comes from the project properties for C# and `sqlproj` projects;
for the other project kinds the project name without its last part is used
(`MyApp.Shared` -> `MyApp`).

A file which more than one project compiles has no target namespace at all: the formula above
gives another answer for every one of these projects. Such a file (a file of a shared project
which is referenced by several projects) is skipped both by the scan and by `CsAdjuster`, see
`WorkspaceHelper.IsCompiledBySeveralProjects`. `XamlAdjuster` asks the same question about the
code behind file of the document (`{name}.xaml.cs`): the `x:Class` of a xaml and the namespace
of its code behind are the two halves of one class and may not be moved separately. The multi target projects (`net48;net8.0`) are
not affected: Visual Studio creates a Roslyn project per target framework, but all of them are
the same project of the solution and have the same project file, and that is what the check
compares. The same holds for the walk through the solution
(`WorkspaceHelper.EnumerateAllDocumentFilePaths`): one file on the disk is one entry of the
list, no matter how many projects compile it.

A file has one target namespace but not necessarily one syntax tree: every project which
compiles it parses it with its own conditional compilation symbols, so `#if NET8_0` is a code
for one of them and a disabled text (a trivia) for another one. Everything which reads the file
therefore works with all of its documents (`WorkspaceHelper.GetDocuments`): the namespace
transitions, the types whose references have to be fixed and the declarations to rename are
collected from every tree of it.

There is one thing which cannot be made consistent that way: the projects may disagree whether
the namespace the file is moved out of stays alive, and the `using` clause of it is then
required by one of them and does not compile for another one. There is a single text for all of
them, so such a file is skipped as well
(`WorkspaceHelper.IsNamespaceStateContradictoryAsync`).

### Scanning (`SubjectFileCollector`)

The collector binds the chosen file paths to their projects (this requires the main thread),
then for every file:

- a xaml file is checked with `XamlAdjuster.IsChangesExistsAsync` (nothing is saved);
- a C# file is checked for the namespace transitions (`NamespaceTransitionContainer`) and for
  the type name conflicts in the target namespace (`NamespaceTypeContainer`). A conflict raises
  `FileProcessException` and stops the scan: such a move would break the compilation.

### Adjusting

`AdjusterFactory` creates an `IAdjuster` for a file:

- **`XamlAdjuster`** rewrites the `x:Class` attribute of the root element. The code behind file
  is processed separately, as a usual C# file.
- **`CsAdjuster`** does the main job:
  1. `NamespaceTransitionContainer.GetNamespaceTransitionsFor` builds the transitions
     (`old namespace -> new namespace`) of the file, over all of its syntax trees;
  2. for every type declared in the file `RefProcessor` finds its references across the solution
     (the type is moved by the transition of the namespace declaration it is written in, see
     `NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf`: only the outermost part
     of a written name is replaced, so `namespace A { namespace B { } }` and `namespace A.B { }`
     in one file are two different transitions of the very same namespace `A.B`)
     (including the usages of its extension methods) and creates a fixer for each of them.
     A file which several projects compile produces a separate symbol per project and Roslyn
     cascades the search to all of them, so the same location is reported once per project:
     the locations are deduplicated by their file and span, and every one of them is analyzed
     against the tree it belongs to (`ReferenceLocation.Document`) and not against the tree of
     the current context of that file;
  3. a fixer for the namespace declarations of the file itself is created;
  4. `FixerContainer.FixAllAsync` applies all the created fixers;
  5. the references to the moved types are fixed in the xaml files of the solution.

### Fixers

A fixer is a modification of one kind in one file. The fixers are accumulated during the
analysis and are applied later, when the whole picture is known: this way a file is parsed and
saved once, no matter how many references it contains.

| Fixer | What it does |
| --- | --- |
| `QualifiedNameFixer` | Rewrites the fully qualified names (`A.B.Class1`, `A.B.Class1.StaticMember`). |
| `AddUsingFixer` | Adds the missing `using` clauses, always among the clauses of the compilation unit. |
| `NamespaceFixer` | Rewrites the namespace declarations of the adjusted file. |

A name we write into a file is resolved relatively to the namespace that file is in, so
`X.Y.Class1` written inside `namespace Some.X` means `Some.X.Y.Class1`. `RefProcessor` asks the
semantic model whether the first part of the target namespace is shadowed at that position and
prefixes the name with `global::` if it is; the same reasoning keeps `AddUsingFixer` out of the
namespace declarations, because a `using` clause written inside one is resolved that way too.

`FixerContainer` groups the fixers by file (`FixerSet`). The order of the fixers inside a set
matters: the qualified names are identified by their spans in the original document, so they
have to be rewritten before any other edit shifts these spans.

`QualifiedNameFixer` and the renaming part of `NamespaceFixer` replace a span of the text and
not a node of a syntax tree. A file may have a tree per project which compiles it, and a name
which is a name in one of them is a part of a disabled text in another one, while the text is
the same for all of them: a span is the only address which is valid everywhere.
The changes of one file must not intersect, so the nested names (`A.B.Outer.Inner` is a
reference to `Outer` and a reference to `Inner` at once) are collapsed to the longest one,
exactly as `SyntaxNode.ReplaceNodes` does it.

Every modification of the Roslyn workspace goes through the `do { ... } while (!TryApplyChanges)`
pattern (see `DocumentChangerHelper`): `Workspace.TryApplyChanges` fails if the solution has been
changed by someone else after our snapshot has been taken, so the change is rebuilt against the
fresh snapshot and applied again.

### Cleanup

`NamespaceCenter` knows all the types of the solution grouped by their namespaces and is
notified about every moved type. A namespace which has lost its last type is remembered, and
`Cleanup.RemoveEmptyUsingStatementsForAsync` removes the using clauses of such namespaces from
every C# document of the solution.

A namespace is emptied for the whole solution, but a `using` clause is resolved against a single
project: a namespace which another project still fills is not empty and is gone for this project
nevertheless (the projects of a solution do not have to reference each other). Therefore both
ends know about the compilation of the document they work with:

- `NamespaceFixer` adds the `using` clause of the old namespace of the adjusted file only if
  that namespace still contains something for the projects of that file
  (`RoslynHelper.IsNamespaceFilledOutside`);
- `NamespaceCenter.GetRemovedNamespaces` removes a clause of a namespace the adjusting has
  touched as soon as that namespace is gone for the given compilations, even if the rest of the
  solution still fills it.

A file which several projects compile has a single text for all of them, so both of these
questions are asked about every project which compiles it and the answers are merged: the clause
stays as soon as a single one of them still needs it.

## The xaml subsystem

A xaml file is processed as a plain text with a set of regexes instead of an XML DOM: this is
the only way to keep the user's formatting untouched.

- `XamlEngine` creates a `XamlDocument` over an `IXamlBodyProvider`:
  `ClosedXamlBodyProvider` works with the file system (fast, not undoable) and
  `OpenedXamlBodyProvider` works with the text buffer of the Visual Studio editor
  (undoable, requires the main thread).
- `XamlDocument` is immutable: every modification produces a new instance, and nothing is
  written back until `SaveIfChangesExistsAgainst` is called. This allows to check whether a file
  is a subject to change without touching it.
- `XamlStructure` holds the interesting fragments of the body with their positions: the xaml
  language alias (`XamlX`), the clr-namespace declarations (`XamlXmlns`), the tags
  (`XamlControl`), the `{x:Type}` / `{x:Static}` markup extensions (`XamlAttributeReference`),
  the `x:Class` attributes (`XamlClass`) and every other `alias:ClassName` pair
  (`XamlTypeUsage`: an attribute value, an attached property, a custom markup extension,
  `x:TypeArguments`). The fragments which may reference a moved class implement
  `IXamlPerformable` and are applied in the backward order, so the earlier positions stay valid.
- The `XamlTypeUsage` scan is a greedy one: it collects everything which looks like an
  `alias:ClassName` pair and is not a part of a fragment recognized above. Such a pair is
  rewritten only if its alias is a clr-namespace one which points to the namespace the class
  is moved out of, so the pairs which reference nothing (`mc:Ignorable="d"`, a time in a text)
  are simply skipped.

## Threading

Visual Studio automation objects (DTE, the solution tree, the editor documents) are available
from the main thread only, so such work is grouped into the separate steps which start with
`ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync`. The heavy Roslyn analysis is moved
to the thread pool with `await TaskScheduler.Default`.

## Logging

`Logging.LogVS` writes into `%TEMP%\AdjustNamespace.vs.log`. It is compiled into the debug builds
only (`[Conditional("DEBUG")]`).
