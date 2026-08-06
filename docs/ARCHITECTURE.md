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
   +--> 3. PerformingViewModel        - shows the progress and cancels,
                                        AdjustSession does the job
```

Both the scan and the adjusting ask one and the same `AdjustPlanner` whether a file is a
subject to change, so the wizard cannot offer a file which the adjusting then silently skips.

The adjusting itself decides first and writes afterwards: the analysis fills an `EditSet`
with a plain description of every change, and `EditApplier` is the only thing which touches
the solution.

### Namespaces of the codebase

| Namespace | Responsibility |
| --- | --- |
| `AdjustNamespace` (the root) | The entry point (`AdjustNamespacePackage`), the context of a run (`AdjustContext`) and the few things everything else uses: `Logging`, `RelayCommand`, `CollectionExtensions`, `TypeContainer`. |
| `AdjustNamespace.Command` | Menu commands. Each of them collects the file paths and opens the wizard. |
| `AdjustNamespace.UI` | The wizard: the place a step is shown in (`IWizardHost`), the steps (`StepFactory`), the viewmodels and the WPF controls. |
| `AdjustNamespace.Adjusting` | The core: the scanner, the adjusters and the final cleanup. |
| `AdjustNamespace.Adjusting.Plan` | The decision what has to happen with a file (`AdjustPlanner`, `AdjustPlanItem`). |
| `AdjustNamespace.Adjusting.Edit` | The decision what has to be written into the files (`EditSet`, `FileEdit`) and, in `.Apply`, the only code which writes it. |
| `AdjustNamespace.Adjusting.Session` | One run over the chosen files (`AdjustSession`): the stages, the progress and the cancellation. |
| `AdjustNamespace.Namespace` | Namespace transitions and the namespace state of the solution. |
| `AdjustNamespace.Xaml` | Reading, modification and saving of the xaml files. |
| `AdjustNamespace.Settings` | Per solution settings stored in the solution folder. |
| `AdjustNamespace.Options` | Per user options stored by Visual Studio. |
| `AdjustNamespace.InfoBar` | The release notes gold bar. |
| `AdjustNamespace.VisualStudio` | The boundary to the IDE: the solution tree, the editor and the project properties, each behind its own interface. |
| `AdjustNamespace.Roslyn` | Everything which is asked of Roslyn itself: the syntax trees (`SyntaxExtensions`), the symbols (`SymbolExtensions`), the workspace (`WorkspaceExtensions`), what may be processed at all (`Scope`) and what is generated code (`GeneratedCode`). |

A folder is a namespace and a namespace is a folder: `AdjustNamespace.Roslyn` is
`AdjustNamespace.VsixShared\Roslyn`, and a file declares the type it is named after. There is no
`Helper` namespace any more — a helper belongs to whatever it is a helper of.

### The boundary to Visual Studio

Everything the extension needs from the running IDE is behind three interfaces of
`AdjustNamespace.VisualStudio`:

| Interface | What it gives | Real implementation |
| --- | --- | --- |
| `ISolutionExplorer` | The files of the solution and the project of every one of them (`ProjectRef`). The tree is walked once per session, see `AdjustPlanner`. | `VsSolutionExplorer` (the solution tree, main thread) |
| `IProjectDefaultNamespaceProvider` | The `DefaultNamespace` property of a project. | `DteProjectDefaultNamespaceProvider` (DTE, main thread) |
| `IDocumentOpener` | Opens a changed file in the editor, which is what makes the change undoable. | `VsDocumentOpener`, or `NullDocumentOpener` when the user has not asked for it |

`AdjustContext` carries these plus the Roslyn workspace and `TargetNamespaceResolver`, and is
created once per command invocation. Everything in it is a real object in the tests as well
(an `AdhocWorkspace` and the fakes of `Tests\AdjustNamespace.Tests\Infrastructure`), so the
core cannot reach the IDE by accident.

The classes of the core take what they really use and not the whole context: `Cleanup`,
`RefProcessor` and the appliers take a Roslyn `Workspace`, `CsAdjuster` and `EditApplier` take
a `Workspace` plus an `IDocumentOpener`, an `EditSet` needs nothing at all and the xaml
subsystem — `XamlAdjuster` included — needs nothing either. Only `AdjustPlanner`,
`AdjusterFactory`, `AdjustSession`, `SubjectFileCollector` and the wizard take the context
itself.

## The wizard

The steps are chained through `IStepFactory<TParameters>`: every step knows the factory of the
next one **and what that step has to be entered with**, so a step wired to a wrong neighbour
does not compile instead of throwing an `InvalidCastException` in the middle of the wizard.
The three parameter types (`PreparationParameters`, `SelectedStepParameters`,
`PerformingParameters`) are the whole contract between the steps.

`WizardChain` builds the chain and is the only place which knows its shape. The chain is not a
line — the second step allows to go back, so the first two steps reference each other and one of
them is necessarily built after the other; this is why the previous step is asked for as a
`Func<>` at the moment the step is created and not at the moment its factory is.

A step does not know the window it lives in. `IWizardHost` is the window as a step sees it —
show this control with this viewmodel, or close the wizard — and `WizardHost` is the
implementation over the `DialogWindow` and its content control. It is also the single place
where an exception of a step is caught and shown in place of the step, so a dead step leaves the
user with the error text and not with an empty window.

1. **`PreparationStepViewModel`** compiles every project of the solution and shows the found
   errors. The adjusting relies on the semantic model, so a broken solution may produce
   incorrect results. The user is allowed to move next anyway.
2. **`SelectedStepViewModel`** runs `SubjectFileCollector` and shows the files which are really
   the subject to change, grouped by their physical folder (`SelectFolderViewModel` +
   `SelectFileViewModel`). Here the user tunes the target namespace regex
   (`NamespaceReplaceRegex`, `KnownRegex`) and decides whether the affected files have to be
   opened in the editor (this is the only way to make the changes undoable).
3. **`PerformingViewModel`** starts an `AdjustSession`, shows what it reports and closes the
   window when it is over. The adjusting itself is not written here: the viewmodel owns the
   `CancellationTokenSource` behind the `Cancel` button and nothing else.

### The session

`AdjustSession` (`Adjusting.Session`) is one run of the extension over the files the user has
chosen. It creates the `NamespaceCenter` shared by all of them, adjusts the files one by one
through `AdjusterFactory` (which asks `AdjustPlanner` again, so a file which stopped being a
subject to change in the meantime is skipped) and finally runs `Cleanup` over every C# document
of the solution.

It reports its position through an `IProgress<AdjustProgress>` — the stage, the file and the
counters, not a ready line of text — and a `CancellationToken` is threaded from there down to
the reference search of Roslyn, which is the longest part of a session. The cancel is answered
between the files and between the types of a file, never while the edits of a file are being
written: an `EditSet` describes a single consistent change and a half of it is a broken file.
A cancelled session is a usual outcome (`AdjustSessionOutcome.Cancelled`) and not an error, and
the changes which have been applied before the cancel are not reverted.

## The core

### Determining the target namespace

The target namespace is `project default namespace` + `folders between the project folder and
the file`, without the folders excluded by the user (`AdjustNamespaceSettings2.IsSkippedFolder`)
and with the user regex applied. The default namespace comes from the project properties for C#
and `sqlproj` projects; for the other project kinds the project name without its last part is
used (`MyApp.Shared` -> `MyApp`).

The rule is split in two along the line where Visual Studio is really needed:

- `TargetNamespaceCalculator` (`Namespace`) is the rule itself and is a computation over the
  paths as strings — no file system, no Roslyn, no main thread. `TryGetFolderChain` gives the
  folders between the project and the file (`null` for a file outside of the project folder),
  `Compose` glues them to the default namespace and applies the regex, and
  `DefaultNamespaceFallback` is the `MyApp.Shared` -> `MyApp` rule. It is covered by the
  automated tests.
- `IProjectDefaultNamespaceProvider` (`VisualStudio`) is the single step which asks Visual
  Studio: the `DefaultNamespace` property of the project, read from the main thread by
  `DteProjectDefaultNamespaceProvider`. It is resolved only after the folder chain has been
  built, so a file which is not going to be adjusted costs no switch to the main thread.

`TargetNamespaceResolver` is the composition of these two. It works over a `ProjectRef` (a name
and a path) and not over a `SolutionItem` of the solution tree, which is what used to make the
whole rule reachable from the main thread only.

A file which more than one project compiles has no target namespace at all: the formula above
gives another answer for every one of these projects. Such a file (a file of a shared project
which is referenced by several projects) is skipped, see
`WorkspaceExtensions.IsCompiledBySeveralProjects`. The same question is asked about the code behind
file of a xaml document (`{name}.xaml.cs`): the `x:Class` of a xaml and the namespace
of its code behind are the two halves of one class and may not be moved separately. The multi target projects (`net48;net8.0`) are
not affected: Visual Studio creates a Roslyn project per target framework, but all of them are
the same project of the solution and have the same project file, and that is what the check
compares. The same holds for the walk through the solution
(`WorkspaceExtensions.EnumerateAllDocumentFilePaths`): one file on the disk is one entry of the
list, no matter how many projects compile it.

A file has one target namespace but not necessarily one syntax tree: every project which
compiles it parses it with its own conditional compilation symbols, so `#if NET8_0` is a code
for one of them and a disabled text (a trivia) for another one. Everything which reads the file
therefore works with all of its documents (`WorkspaceExtensions.GetDocuments`): the namespace
transitions, the types whose references have to be fixed and the declarations to rename are
collected from every tree of it.

There is one thing which cannot be made consistent that way: the projects may disagree whether
the namespace the file is moved out of stays alive, and the `using` clause of it is then
required by one of them and does not compile for another one. There is a single text for all of
them, so such a file is skipped as well
(`WorkspaceExtensions.IsNamespaceStateContradictoryAsync`).

### The decision (`AdjustPlanner`)

Everything above is a rule about a single file, and all of these rules live in one place.
`AdjustPlanner.TryPlanAsync` answers with an `AdjustPlanItem` — the file, its target namespace,
its kind and, for a C# file, its namespace transitions — or with `null`, which means the file
has to be left alone:

- it belongs to no project of the solution;
- its target namespace cannot be determined (a file outside of its project folder);
- it is no C# document of the workspace (`Predicate.IsDocumentInScope`);
- several projects compile it (or, for a xaml file, its code behind);
- it has no namespace transition at all: it is in the target namespace already;
- the projects which compile it disagree about the namespace it is moved out of.

The plan is the whole contract between the two steps of the wizard: the scan shows the files
the planner accepts and the adjusters perform the plans it produced. Previously both of them
decided on their own, and an adjuster which disagreed with the file list simply returned `false`
and left the user with a file which had been offered and was not changed.

The solution tree is asked for once per planner (`ISolutionExplorer.GetProjectOfEveryFileAsync`)
and not once per file, so a session switches to the main thread for it a single time.

### Scanning (`SubjectFileCollector`)

The collector asks the planner about every file chosen by the user and adds on top of that
what only this step needs:

- a planned xaml file is checked with `XamlAdjuster.IsChangesExistsAsync` (nothing is saved):
  whether the root class really moves is known after the document has been read;
- a planned C# file is checked for the type name conflicts in the target namespace
  (`NamespaceTypeContainer`). A conflict raises `FileProcessException` and stops the scan:
  such a move would break the compilation and cannot be undone by the adjusting itself.

### Adjusting

`AdjusterFactory` creates the `IAdjuster` which performs a plan (and decides nothing itself):

- **`XamlAdjuster`** rewrites the `x:Class` attribute of the root element. The code behind file
  is processed separately, as a usual C# file.
- **`CsAdjuster`** does the main job, over the transitions the plan carries:
  1. for every type declared in the file `RefProcessor` finds its references across the solution
     (the type is moved by the transition of the namespace declaration it is written in, see
     `NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf`: only the outermost part
     of a written name is replaced, so `namespace A { namespace B { } }` and `namespace A.B { }`
     in one file are two different transitions of the very same namespace `A.B`)
     (including the usages of its extension methods) and schedules an edit for each of them.
     A file which several projects compile produces a separate symbol per project and Roslyn
     cascades the search to all of them, so the same location is reported once per project:
     the locations are deduplicated by their file and span, and every one of them is analyzed
     against the tree it belongs to (`ReferenceLocation.Document`) and not against the tree of
     the current context of that file;
  2. an edit for every root namespace declaration of the file itself is scheduled;
  3. `EditApplier.ApplyAsync` writes the whole set;
  4. the references to the moved types are fixed in the xaml files of the solution.

### The edits

An `EditSet` is everything an adjusting is going to change, grouped by file. It is filled
during the analysis and applied afterwards, when the whole picture is known: this way a file is
parsed and saved once, no matter how many references it contains.

An edit is a plain description of a change and knows neither the workspace nor the documents,
so a decision may be built, inspected and asserted without touching the solution.

| Edit | What it means |
| --- | --- |
| `ReplaceTextEdit` | Rewrite a span of the text: a fully qualified name (`A.B.Class1`, `A.B.Class1.StaticMember`). |
| `AddUsingEdit` | Import a namespace, always among the `using` clauses of the compilation unit. |
| `MoveNamespaceEdit` | Move a namespace declared in the file: rename every declaration of it and import its old name. |

The duplicates are dropped by the set itself: a file which references the moved type ten times
needs one `using` clause and not ten.

A name we write into a file is resolved relatively to the namespace that file is in, so
`X.Y.Class1` written inside `namespace Some.X` means `Some.X.Y.Class1`. `RefProcessor` asks the
semantic model whether the first part of the target namespace is shadowed at that position and
prefixes the name with `global::` if it is; the same reasoning keeps `AddUsingApplier` out of
the namespace declarations, because a `using` clause written inside one is resolved that way too.

`EditApplier` walks the set file by file, and the order of the kinds inside a file matters:
a `ReplaceTextEdit` is identified by its span in the original text, so all of them are written
before any other edit shifts these spans. Every kind has its own applier
(`ReplaceTextApplier`, `AddUsingApplier`, `MoveNamespaceApplier`) and all the edits of one kind
are written as a single change of the document.

A `ReplaceTextEdit` and the renaming part of a `MoveNamespaceEdit` replace a span of the text
and not a node of a syntax tree. A file may have a tree per project which compiles it, and
a name which is a name in one of them is a part of a disabled text in another one, while the
text is the same for all of them: a span is the only address which is valid everywhere.
The changes of one file must not intersect, so the nested names (`A.B.Outer.Inner` is a
reference to `Outer` and a reference to `Inner` at once) are collapsed to the longest one,
exactly as `SyntaxNode.ReplaceNodes` does it.

Every modification of the Roslyn workspace goes through the `do { ... } while (!TryApplyChanges)`
pattern (see `DocumentChanger`): `Workspace.TryApplyChanges` fails if the solution has been
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

- `MoveNamespaceApplier` adds the `using` clause of the old namespace of the adjusted file only
  if that namespace still contains something for the projects of that file
  (`SymbolExtensions.IsNamespaceFilledOutside`);
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
