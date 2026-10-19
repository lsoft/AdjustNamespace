# Architecture

This document describes how AdjustNamespace is arranged inside. It is intended for the
contributors; if you are looking for the user documentation, please read [../README.md](../README.md).

## Projects

| Project | Contents |
| --- | --- |
| `AdjustNamespace.CoreShared` | A shared MSBuild project with the core: the planner, the adjusters, the edits, the session, the xaml subsystem, the settings and the interfaces of the boundary to the IDE. It knows nothing about Visual Studio and is compiled by every host below. |
| `AdjustNamespace.VsixShared` | A shared MSBuild project with everything which needs the running IDE: the wizard, the menu commands, the options, the info bar and the implementations of the boundary interfaces. |
| `AdjustNamespace.2022` | The VSIX project for Visual Studio 2026 (the name is historical): the manifest, the command table (`VSCommandTable.vsct`), the image manifest and the resources. Imports both shared projects. |
| `AdjustNamespace.Cli` | The console utility `adjustns` (`net10.0`): the core over an `MSBuildWorkspace`. Imports `AdjustNamespace.CoreShared` only. |
| `Tests/AdjustNamespace.Tests` | The automated tests (`net48`, xunit). Imports both shared projects, see [../Tests/README.md](../Tests/README.md). |

The line between the two shared projects is the whole point of the split: a class of the core
compiles into a .NET Framework 4.8 extension and into a .NET 10 console tool at once, so it may
use neither the Visual Studio SDK nor an API which exists in one of the two Roslyn versions
only. A new file of the core is added to `AdjustNamespace.CoreShared.projitems`, a new file of
the IDE part to `AdjustNamespace.VsixShared.projitems`.

The extension targets .NET Framework 4.8 and is built against
[Community.VisualStudio.Toolkit](https://github.com/VsixCommunity/Community.VisualStudio.Toolkit),
Roslyn (`Microsoft.CodeAnalysis.*`) and `Microsoft.VisualStudio.LanguageServices`.
It supports Visual Studio 2026 (18.x) only and is compiled against the Roslyn of 18.0, the
lowest supported release: Visual Studio binds an older Roslyn reference to its own one, never
a newer one. There is no code which depends on the Visual Studio version.

The post-build event of `AdjustNamespace.2022` refreshes the `Tests/Subject` folder from
`Tests/Standard`, see [../Tests/README.md](../Tests/README.md).

## The big picture

The core has two hosts. Visual Studio drives it through the wizard:

```
 Command (AdjustNamespaceCommand / AdjustSolutionCommand / AdjustSelectedCommand)
   |  collects the file paths chosen by the user
   v
 AdjustNamespaceWindow (modal wizard)
   |
   +--> 1. PreparationStepViewModel   - compiles the solution, reports the errors
   |
   +--> 2. SelectedStepViewModel      - SubjectFileCollector scans the files,
   |                                    the user chooses what to adjust and how
   |                                    aggressively the text references are searched
   |
   +--> 2a. ReviewStepViewModel       - the text references found, each with a check box
   |                                    (skipped when there is nothing to show)
   |
   +--> 3. PerformingViewModel        - shows the progress and cancels,
   |                                    AdjustSession does the job
   |
   +--> 4. ReportStepViewModel        - shows the AdjustReport of the run
```

and the console utility walks the very same three steps without asking anybody:

```
 Program (adjustns)
   |  parses the command line (CliOptions)
   v
 AdjustCommand
   |
   +--> 1. MsBuildSolutionLoader     - opens the .sln/.slnx/.csproj, compiles it,
   |                                   stops on the errors unless --force
   |
   +--> 2. SubjectFileCollector      - the same scan; --dry-run and --check stop here
   |
   +--> 3. AdjustSession             - the same job, the progress goes to the console
   |
   +--> 4. AdjustReport              - printed on the console, saved by --report
```

Both the scan and the adjusting ask one and the same `AdjustPlanner` whether a file is a
subject to change, so the wizard cannot offer a file which the adjusting then silently skips.

The adjusting itself decides first and writes afterwards: the analysis fills an `EditSet`
with a plain description of every change, and `EditApplier` is the only thing which touches
the solution.

### Namespaces of the codebase

| Namespace | Responsibility |
| --- | --- |
| `AdjustNamespace` (the root) | The entry point (`AdjustNamespacePackage`), the context of a run (`AdjustContext`) and the few things everything else uses: `Logging`, `RelayCommand`, `CollectionExtensions`, `TypeContainer`, `FileEx`. |
| `AdjustNamespace.Command` | Menu commands. Each of them collects the file paths and opens the wizard. |
| `AdjustNamespace.UI` | The wizard: the place a step is shown in (`IWizardHost`), the steps (`StepFactory`), the viewmodels and the WPF controls. |
| `AdjustNamespace.Adjusting` | The core: the scanner, the adjusters and the final cleanup. |
| `AdjustNamespace.Adjusting.Plan` | The decision what has to happen with a file (`AdjustPlanner`, `AdjustPlanItem`). |
| `AdjustNamespace.Adjusting.Edit` | The decision what has to be written into the files (`EditSet`, `FileEdit`) and, in `.Apply`, the only code which writes it. |
| `AdjustNamespace.Adjusting.Session` | One run over the chosen files (`AdjustSession`): the stages, the progress and the cancellation. |
| `AdjustNamespace.TextReferences` | The search of the names of the moved types that are written as a text, the confidence score of a match and the application of the approved ones. |
| `AdjustNamespace.Adjusting.Report` | What a run has done (`AdjustReport`, built by `AdjustReportBuilder`) and the renderers of it: Markdown, JSON and the tree of the wizard. |
| `AdjustNamespace.Namespace` | Namespace transitions and the namespace state of the solution. |
| `AdjustNamespace.Xaml` | Reading, modification and saving of the xaml files. |
| `AdjustNamespace.Settings` | Per solution settings stored in the solution folder. |
| `AdjustNamespace.Options` | Per user options stored by Visual Studio. |
| `AdjustNamespace.InfoBar` | The release notes gold bar. |
| `AdjustNamespace.VisualStudio` | The boundary to the IDE: the interfaces (in the core) and their Visual Studio implementations (in `AdjustNamespace.VsixShared`). |
| `AdjustNamespace.Roslyn` | Everything which is asked of Roslyn itself: the syntax trees (`SyntaxExtensions`), the symbols (`SymbolExtensions`), the workspace (`WorkspaceExtensions`), what may be processed at all (`Scope`) and what is generated code (`GeneratedCode`). |
| `AdjustNamespace.Cli` | The console utility: the command line (`CommandLine`), the run itself (`AdjustCommand`) and the answers to the boundary interfaces built out of the MSBuild data (`MsBuild`). |

A folder is a namespace and a namespace is a folder: `AdjustNamespace.Roslyn` is
`AdjustNamespace.CoreShared\Roslyn`, and a file declares the type it is named after. There is no
`Helper` namespace any more — a helper belongs to whatever it is a helper of.

### The boundary to Visual Studio

Everything the core needs from the outside world is behind these interfaces of
`AdjustNamespace.VisualStudio` and `AdjustNamespace.Xaml.BodyProvider`. The interfaces live in
the core, the implementations belong to the host:

| Interface | What it gives | In Visual Studio | In the console utility |
| --- | --- | --- | --- |
| `ISolutionExplorer` | The files of the solution and the project of every one of them (`ProjectRef`). The tree is walked once per session, see `AdjustPlanner`. Also the text files that may name a type (`GetTextFilePathsAsync`): the project files, the configs, the Razor views. | `VsSolutionExplorer` (the solution tree, main thread) | `MsBuildSolutionExplorer` (the documents of the workspace plus the xaml files found on the disk) |
| `IProjectDefaultNamespaceProvider` | The `DefaultNamespace` property of a project. | `DteProjectDefaultNamespaceProvider` (DTE, main thread) | `ProjectFileDefaultNamespaceProvider` (`Project.DefaultNamespace` of Roslyn, i.e. the `RootNamespace` of the project file) |
| `IXamlBodyProviderFactory` | The way a xaml file is read and written. Reads always go through the file system; writes in Visual Studio go through an invisible text buffer so they participate in the global linked undo. | `VsXamlBodyProviderFactory` | `ClosedXamlBodyProviderFactory`: always the file system |

`AdjustContext` carries these plus the Roslyn workspace and `TargetNamespaceResolver`, and is
created once per run: by `VsAdjustContext.CreateAsync` (which is the only place asking the IDE
for the DTE and the `VisualStudioWorkspace`) in the extension and by `AdjustCommand` over an
`MSBuildWorkspace` in the console utility. Everything in it is a real object in the tests as well
(an `AdhocWorkspace` and the fakes of `Tests\AdjustNamespace.Tests\Infrastructure`), so the
core cannot reach the IDE by accident.

The classes of the core take what they really use and not the whole context: `Cleanup`,
`RefProcessor` and the appliers take a Roslyn `Workspace`, `CsAdjuster` and `EditApplier` take
a `Workspace`, an `EditSet` needs nothing at all and the xaml
subsystem — `XamlAdjuster` included — needs the body-provider factory only. Only `AdjustPlanner`,
`AdjusterFactory`, `AdjustSession`, `SubjectFileCollector` and the wizard take the context
itself.

## The wizard

The steps are chained through `IStepFactory<TParameters>`: every step knows the factory of the
next one **and what that step has to be entered with**, so a step wired to a wrong neighbour
does not compile instead of throwing an `InvalidCastException` in the middle of the wizard.
The parameter types (`PreparationParameters`, `SelectedStepParameters`, `ReviewParameters`,
`PerformingParameters`, `ReportParameters`) are the whole contract between the steps.

`WizardChain` builds the chain and is the only place which knows its shape. The chain is not a
line — the second step allows to go back, and so does the review step, so these steps reference
each other and one of them is necessarily built after the other; this is why the previous step
is asked for as a `Func<>` at the moment the step is created and not at the moment its factory is.

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
   (`NamespaceReplaceRegex`, `KnownRegex`).
   The same step has a collapsible **Text references** block: the aggressiveness combo box
   (`TextModeChoice`) and a check box per kind of file. They start from the user options
   (`General`, Tools -> Options) overridden by the settings of the solution, and what the user
   chooses here is handed back to the step if the review sends them back (`SelectedStepParameters`).
   "Next" runs the search (`TextReferenceScanner`, the progress goes to the status line) and enters
   the review step with the result (`ReviewParameters`).
   **`ReviewStepViewModel`** shows the text references of the scan grouped by file: a level badge
   (colored by the level), the line with the name highlighted, `old -> new`, a check box that is
   the `IsApproved` of the match itself (preset by the mode, so what the session reads is what the
   user sees), the signals of the score as a tooltip, a filter by level (the unlikely ones are
   hidden unless asked for), "Check all Likely", "Check all" and "Uncheck all", and a double click
   that opens the file at the line (`FileNavigator`). `ReviewStepFactory` shows the step only if
   the scan has something to show; otherwise it goes straight to the run. "Next" starts the
   session with the reviewed `TextReferenceScanResult`, so the session does not search again.
3. **`PerformingViewModel`** opens a global linked undo transaction (`mdtGlobal`), starts an
   `AdjustSession`, shows what it reports and, when it is over, closes the transaction and
   moves on to the report. The adjusting itself is not written here: the viewmodel owns the
   `CancellationTokenSource` behind the `Cancel` button and the undo transaction. A failed
   session (`AdjustSessionOutcome.Failed`) aborts the transaction, as an exception used to.
4. **`ReportStepViewModel`** shows the `AdjustReport` of the run instead of the automatic close:
   the summary line, a tree of the sections (`AdjustReportSections`, built in the core so the
   viewmodel has nothing to decide), and the buttons `Copy as Markdown`, `Save report...`,
   `Open log` and `Close`. Whether the step is shown is the option `General.ShowReport`
   (always / only if something is left / never); a failed run is always shown.

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

When the search of the text references is on (see [Text references](#text-references)), the
session starts with it: it searches by itself (`AdjustStage.TextReferences` is reported) or
takes the `TextReferenceScanResult` the wizard has made and the user has reviewed, and changes
the approved matches before the first file is adjusted. A session built without the options
does not search at all, which is what the tests of the adjusting itself rely on.

`RunAsync` answers with an `AdjustReport` (the outcome is its `Outcome` property). An unexpected
failure while a file is processed is caught there as well: the session stops, the report is
`AdjustSessionOutcome.Failed` and names the file and the message, and the files behind it are
not processed. What a caller does about it is its own business: the wizard rolls the undo
transaction back, the console utility prints the message and exits with a failure.

### The run report

`AdjustReport` (`Adjusting.Report`) is a plain data object of the core, which says what a run
has done:

- per file the user wanted adjusted an `AdjustFileOutcome`: adjusted (with the old and the new
  namespaces, the files its adjusting has changed and what has been done in each of them),
  skipped (nothing to move), blocked (with the `AdjustBlock` and its reason), not processed (the
  run was cancelled or failed before the file) or failed (with the message). The files the scan
  before the session has found impossible to adjust are given to the session and are in the
  report as blocked;
- the cleanup apart: the using clauses it has removed and the `nameof` it has replaced, per file;
- every touched file once, with all the edits of it, by `EditKind` (using added, name
  qualified, `global::` added, namespace declaration moved, `XmlnsDefinition`, `x:Class`, xaml
  references and xmlns, `nameof` replaced, using removed by the cleanup).

The adjusters and the cleanup do not know the report; they are given an optional
`AdjustReportBuilder` and tell it what they are about to write (`CsAdjuster` the whole `EditSet`
of a file right before `EditApplier` writes it, `XamlAdjuster` the `x:Class`, `Cleanup` the
removals). The kind of a `ReplaceTextEdit` is a property of the edit and no part of its
identity, so the report is a view of the decision and not a second source of truth.

The report renders itself: `ToMarkdown()` (`AdjustReportMarkdown`) and `ToJson()`
(`AdjustReportJson`, over a small indenting writer of its own, because the core is compiled for
.NET Framework 4.8 and for .NET 10 and a JSON library would be one more dependency of the
extension). The wizard shows `AdjustReportSections.Build(report)`, a tree of `ReportItem`, the
console utility prints the Markdown, and `Copy as Markdown` and `--report` give the very same
text. The paths are shown relative to the folder of the solution (`ReportPaths`).

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
`AdjustPlanner.PlanAsync` answers with an `AdjustPlanResult`:

- **Plan** — an `AdjustPlanItem` (the file, its target namespace, its kind and, for a C# file,
  its namespace transitions);
- **Block** — an `AdjustBlock` with a reason the wizard and the console utility show to the
  user (no project, unknown target namespace, not a processable document, compiled by several
  projects, contradictory TFM namespace state, xaml whose code behind is multi-project, a
  namespace a project file imports with `<Using Include>` which the move would empty);
- **None** — the file is already fine (in the target namespace already) and is dropped silently.
  A xaml whose `x:Class` names a class declared in a C# file is no subject either: the
  `x:Class` belongs to that class and follows it when the C# file moves, whatever folder the
  xaml lies in.

`TryPlanAsync` remains as a thin wrapper that returns the plan or `null` for callers that only
need to know whether the file is adjusted.

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
  whether the root class really moves is known after the document has been read; no change is
  a silent drop;
- a planned C# file is checked for the type name conflicts in the target namespace
  (`NamespaceTypeContainer`). A conflict becomes an `AdjustBlock` for that file only: other
  adjustable files of the same scan stay collected. Types of collected files are reserved so
  two subject files cannot both land the same name into the same target; a reservation
  remembers its type, so the other part of the same partial type is no conflict, and the
  file-local types of C# 11 are no conflict at all;
- the collected files are checked as a whole: a file whose partial type has a part which is
  not collected or would land in another namespace is blocked (a form and its `.Designer.cs`
  have to move together), and so are the files which only together empty a namespace a project
  file imports.

`SubjectCollectingResults` therefore carries `CollectedFiles` and `Blocked`. An unexpected
failure while deciding still raises `FileProcessException` and aborts the scan.

### Adjusting

`AdjusterFactory` creates the `IAdjuster` which performs a plan (and decides nothing itself):

- **`XamlAdjuster`** rewrites the `x:Class` attribute of the root element of a xaml whose class
  is not declared in a C# file (a xaml with a code behind is no subject, see the planner).
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
  2. `SelfReferenceFixer` schedules the edits of the references the file itself makes through
     its old enclosing namespaces (a type, an extension member, a child namespace such as
     `Properties`, a relative `using Fourth;` inside the namespace declaration), of the names
     its new namespace hides (`First.Second.Thing` inside `Target.First` becomes
     `global::First.Second.Thing`) and of the type names which would mean another type or
     become ambiguous in the new namespace (they get the namespace of their type in front of
     them), and `XmlnsDefinitionFixer` the edits of the
     `[assembly: XmlnsDefinition(uri, "A.B")]` attributes of the namespaces the types leave;
  3. an edit for every root namespace declaration of the file itself is scheduled;
  4. `EditApplier.ApplyAsync` writes the whole set;
  5. the references to the moved top-level types are fixed in the xaml files of the solution
     (a nested type is written through its outer type there).

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
| `ReplaceFileTextEdit` | Rewrite a span of the text of a file that is no C# document (a config, a Razor view, a project file). The file is never parsed and written back through a DOM, so its formatting, encoding, BOM and line breaks stay as they are. It is written through the body provider of the host (`IXamlBodyProviderFactory`, which despite its name works with any text file), i.e. an invisible text buffer in Visual Studio, so the global undo covers it. |

The duplicates are dropped by the set itself: a file which references the moved type ten times
needs one `using` clause and not ten.

A name we write into a file is resolved relatively to the namespace that file is in, so
`X.Y.Class1` written inside `namespace Some.X` means `Some.X.Y.Class1`. `RefProcessor` asks the
semantic model whether the first part of the target namespace is shadowed at that position and
prefixes the name with `global::` if it is; the same reasoning keeps `AddUsingApplier` out of
the namespace declarations, because a `using` clause written inside one is resolved that way too.

The semantic model answers for the place a name is written at now, and the moved file is going
to be somewhere else. `NameLookup` repeats the lookup of C# for a code written in another
namespace: the members of every enclosing namespace from the innermost one (the namespaces of
the new chain count even before they exist), then the using clauses (`GetImportScopes`) plus the
ones the adjusting adds. A name whose lookup gives another symbol, or several ones, is written
out in full. `RefProcessor` uses the same question the other way round: a `using` clause of the
target namespace which would make another simple name of the file ambiguous (CS0104) is not
added, and the reference to the moved type is qualified instead.

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
every C# document of the solution, and replaces a `nameof` of such a namespace with the string
it gives (`nameof(First.Second.Third)` becomes `"Third"`): the program keeps seeing the same
string, which a rewritten `nameof` would change.

A namespace is emptied for the whole solution, but a `using` clause is resolved against a single
project: a namespace which another project still fills is not empty and is gone for this project
nevertheless (the projects of a solution do not have to reference each other). Therefore both
ends know about the compilation of the document they work with:

- `MoveNamespaceApplier` adds the `using` clause of the old namespace of the adjusted file only
  if that namespace still contains something for the projects of that file
  (`SymbolExtensions.IsNamespaceFilledOutside`); only the types declared *directly* in the
  namespace count — types of the child namespaces are invisible to a `using` of the parent,
  so counting them would add a clause which later fails to compile. Neither the generated part
  of a moved class counts nor a type the build generates out of the xaml of the moved code
  behind (`Program` of WinUI in `obj\...\App.g.i.cs`, `GeneratedCode.IsGeneratedOutOfXamlOf`):
  both are written into the namespace of the `x:Class`, which follows the class;
- `NamespaceCenter.GetRemovedNamespaces` removes a clause of a namespace the adjusting has
  touched as soon as that namespace has no direct types left for the given compilations, even
  if the rest of the solution still fills it (or a child namespace of it still exists).

A file which several projects compile has a single text for all of them, so both of these
questions are asked about every project which compiles it and the answers are merged: the clause
stays as soon as a single one of them still needs it.

### The build check and the Error List

Two things keep what a run has not fixed in sight after the window is closed.

- **The build check** (`Adjusting.BuildChecking`, optional: the option `General.RunBuildCheck`,
  `--build-check` of the console utility, the `runBuildCheck` argument of `AdjustSession`).
  `BuildChecker.CaptureAsync` collects the compilation errors of the C# projects from the
  compilations of the workspace (the thing the adjusting relies on anyway: no build, no output
  folder, no main thread) before the run; after it the projects the run has affected are
  recomputed (`AffectedProjectNames`: the project of every changed file — a C# document or a file
  inside a project folder — and everything that transitively depends on it) and `Compare` tells
  the new errors. An error is the same error before and after when the project, the diagnostic id
  and the message are the same: the position is no part of it, because the run moves the lines.
  One thing the workspace gets wrong is compensated for: it reads the project file when the solution
  is opened and never again, so a `StartupObject` the run has changed would still be looked for
  under its old name (CS1555, a false "new error"); the "after" snapshot compiles that project
  with the new `MainTypeName`.
  The result is `AdjustReport.BuildCheck`; new errors are listed with their file and line (the
  wizard opens them on a double click) and make the report `HasLeftovers`.
- **The Error List.** `AdjustReportErrorList.Build` decides what goes there — one warning for every
  text reference the run has left and one for every new compilation error, each at its file and
  line — and `ErrorListReporter` (Visual Studio only, an `ErrorListProvider`) replaces the entries
  of the previous run with them. `PerformingViewModel` calls it as soon as the run is over, so the
  entries are there whether the report step is shown or not.

## Text references

Roslyn sees the references to a type that are written as code. A name written as a text is out of
its sight and keeps the old name after the move: `Type.GetType("A.B.C, MyAsm")`, a comment, the
`type="..."` attribute of a config file. `AdjustNamespace.TextReferences` finds them, gives every
match a **confidence score** and changes the ones the user's mode trusts.

Only the **full names** of the moved types are searched, never a short name (`Customer`,
`Models` are far too common), and a name has borders (`X.A.B.C` and `A.B.CExtra` are not
`A.B.C`; `A.B.C.Member` is). The forms are `A.B.C`, `global::A.B.C`, the metadata forms
`A.B.Outer+Inner` and ``A.B.C`1``, and the assembly qualified `A.B.C, MyAsm, Version=...`; the
replacement is the same form with the new namespace.

### The pipeline

```
 plans of the session ──> MovedTypeCollector ──> MovedTypeSet
                                                    │
 C# documents ─────────> CSharpTextScanner ──┐      │
 (other files, see below)  scanners         ─┼──> TextCandidate ──> TextReferenceAnalyzer ──> TextReferenceMatch
                                             │     (a part of a file     (finds the names,        (span, line, score,
                                                    that may name a type)  scores them)            signals)
 TextReferenceScanner ───────────────────────────────> TextReferenceScanResult ──> TextReferenceApplier ──> EditSet
```

- **`MovedTypeCollector`** learns which top level types the session moves and where to out of the
  plans of the files, with the very rules `CsAdjuster` follows (`NamespaceTransitionContainer`).
  Nothing has been changed yet, so the search runs *before* the adjusting. It also replays the
  moves on a `NamespaceCenter` to learn which namespaces are emptied, and asks the compilation
  whether a type (or a namespace) of that full name exists in another referenced assembly.
  The result is a `MovedTypeSet`: the types indexed by their old full name, and the namespaces.
  A nested type moves with its outer one and is known by its metadata name only.
- **A scanner** finds the *candidates*, the parts of a file that may name a type, and knows
  nothing about the moved types: `CSharpTextScanner` walks the tokens of a syntax tree (string
  literals of every kind — regular, verbatim, raw, UTF-8, the text parts of an interpolated
  string — and, if asked for, comments and the text of documentation comments, but not a `cref`,
  which Roslyn handles). A string that is passed as a type name (`Type.GetType`,
  `Assembly.GetType`, `Activator.CreateInstance`, `AppDomain.CreateInstance*`,
  `new ResourceManager(baseName, ...)`, `[TypeConverter]`, `[Designer]`, `[Editor]`,
  `[DebuggerTypeProxy]`, `[TypeForwardedFrom]`, any parameter named `typeName`,
  `assemblyQualifiedName`, `className` or `baseName`) is told from the others through the
  semantic model; without one the names of the methods are all there is. The spans are exact:
  the raw text of the file is searched, not the value of a literal.
- The other files each have a scanner of their own, which produces candidates the same way:
  `RazorTextScanner` (the directive lines `@using`, `@model`, `@inherits`, `@inject`,
  `@typeparam`, `@implements`, `@namespace` of `.cshtml` and `.razor` files, `_ViewImports`
  included; a generic argument is in the rest of the line, which is searched whole),
  `ProjectTextScanner` (`<StartupObject>` and the `CustomToolNamespace` of a `.csproj`; the
  `GeneratedClassNamespace` and the `Type` of a setting of a `.settings` file). They read the text
  with `XmlTokenizer`, a small positioned reader of xml: `XmlReader` knows the line of a node but
  not where the value of an attribute starts, and a file has to be changed where it is written,
  never re-serialized through a DOM. The tokenizer is tolerant, so a config that is not a well
  formed xml is read as far as it goes.
  `XmlConfigTextScanner` (the attributes `type`, `typeName`, `mapTo`, `contract`, `factory` — with
  or without a prefix, so `xsi:type` counts — and the `name` of a `service`, plus the text of
  elements called so; every other attribute value and text is a plain value; xml comments only if
  asked for) and `JsonTextScanner` (the string values of a json file with exact spans, the value of
  `$type`, `type`, `class` and the elements of `Using` in any case are type values; the keys are not
  searched; `//` and `/* */` comments are skipped). The json reader is hand written because the
  core is compiled for .NET Framework 4.8 as well and `Utf8JsonReader` would be a dependency. A
  match under `$type` carries a note, and the report prints it: data serialized with the old name
  may exist elsewhere and cannot be fixed by an edit of the sources.
  A **namespace** is a candidate where it can be named (a Razor `@using` / `@namespace`,
  `GeneratedClassNamespace`, `CustomToolNamespace`): it is changed only if the session empties
  it and all of its types go to the same place (`MovedNamespace.IsEmptied` / `NewName`), and
  never gets above 89 ("not certain"); a namespace that stays alive or is split between several
  places is not a match at all.
- **Which files** are searched: `ISolutionExplorer.GetTextFilePathsAsync` (the solution tree and
  the project files in Visual Studio, a walk through the folders of the projects without `bin`,
  `obj`, `node_modules`, `.git` and `.vs` in the console utility) and `TextReferenceFiles.Classify`
  (the extension tells the kind, the switches of the options say whether the kind is searched).
  Unlike the namespace chain, the skipped folders of `adjust_namespaces_settings.xml` are *not*
  excluded here: they are folders that do not take part in the namespaces, and `Properties`,
  the usual one, holds the `.settings` files.
- **`TextReferenceAnalyzer`** looks for the names in a candidate (`NameMatcher`: one regex pass
  over the dotted identifier chains and a dictionary lookup of the longest prefix, so a thousand
  moved types cost no more than one) and scores what it finds.
- **`TextReferenceScanner`** puts it together (`ScanAsync`), numbers the matches and presets the
  approval of every one by the mode. A file that names none of the types is not parsed at all.
- **`TextReferenceApplier`** turns the approved matches into the edits of an `EditSet` (a C#
  document gets a `ReplaceTextEdit`, any other file a `ReplaceFileTextEdit`) and `EditApplier`
  writes it, as everywhere else. A match is changed only if the text at its span is still the
  name that was found: a text that has changed since the search (the user typed, a cancelled run
  left files behind) is reported as left, never corrupted.

The session changes the text references *first*, before the first file is adjusted: the spans of
the search are valid against the text as it is, and a cancelled run leaves the strings in the
state the next run finds the types in.

### The score

Every match has a score 0..100 that is a **list of named signals with weights** (`ConfidenceScore`,
`ConfidenceSignal`, the weights are in `ScoreSignals`): the place the name is found in is the base
(a Razor directive 100, a string passed as a type name 95, an xml `type` attribute 85, a json
`$type` 80, a string literal that is the name as a whole 60, an xml or json value 50, a comment 40,
a name inside a longer string 30) and the modifiers add to it (the assembly of the moved type +20,
another assembly -40, the real metadata form +10, exactly the name +10, prose around it -10,
looks like a file or a URL -40, a type of that name in another assembly -30, test data -20; a
namespace that is emptied never gets above 89, one that stays alive is not a match at all). The
signals are data so that the review step can show "why this score" as a tooltip and the report
prints it. The levels are ranges of the score: Certain 90..100, Likely 70..89, Possible 45..69,
Unlikely 0..44 (hidden). The weights are a starting point checked by
`TextReferenceScoringCorpusTests`: every example of the corpus has to land in its expected level.

### The mode

| Mode | Scan | Changed | Offered (unchecked) |
| --- | --- | --- | --- |
| `Off` | no | | |
| `ReportOnly` | yes | nothing | from Possible |
| `Conservative` | yes | Certain | Likely, Possible |
| `Balanced` (default) | yes | Certain, Likely | Possible |
| `Aggressive` | yes | Certain, Likely, Possible | |

plus a switch per kind of text (`TextReferenceKinds`: comments, xml configs, json, Razor, project
and settings files). The defaults are the user options in Visual Studio and the command line of
the console utility; the settings file of the solution overrides them
(`TextReferenceOptions.Resolve`: new optional elements of `AdjustNamespaceSettings`, an older
file without them overrides nothing, and every element is nullable so "not said" is not "false").

## The xaml subsystem

A xaml file is processed as a plain text with a set of regexes instead of an XML DOM: this is
the only way to keep the user's formatting untouched. Both classic `.xaml` (WPF, MAUI, …)
and Avalonia `.axaml` are recognized (`XamlPathHelper`).

- `XamlEngine` creates a `XamlDocument` over an `IXamlBodyProvider`, and which one it is
  is decided by the `IXamlBodyProviderFactory` of the context:
  `CreateForReadAsync` always uses the file system (fast probes),
  `CreateForWriteAsync` uses `InvisibleXamlBodyProvider` in Visual Studio (an invisible text
  buffer that participates in the global linked undo, no editor tab) and
  `ClosedXamlBodyProvider` elsewhere. The invisible provider lives in `AdjustNamespace.VsixShared`:
  the core knows the interface and never the editor.
- `XamlDocument` is immutable: every modification produces a new instance, and nothing is
  written back until `SaveIfChangesExistsAgainst` is called. This allows to check whether a file
  is a subject to change without touching it.
- `XamlStructure` holds the interesting fragments of the body with their positions: the xaml
  language alias (`XamlX`, including the MAUI 2009 uri and the implicit `x` of a document which
  declares none), every namespace declaration with the element it is written on
  (`XamlXmlns`: the CLR mappings `clr-namespace:` and `using:`, and the other ones, which may
  shadow a mapping), the tags with and without a prefix (`XamlControl`), the `{x:Type}` /
  `{x:Static}` markup extensions (`XamlAttributeReference`), the `x:Class` attributes
  (`XamlClass`) and every other `alias:ClassName` pair (`XamlTypeUsage`: an attribute value,
  an attached property, a custom markup extension, `x:TypeArguments`, an Avalonia selector
  `alias|ClassName`). The fragments which may reference a moved class implement
  `IXamlPerformable` and are applied in the backward order, so the earlier positions stay valid.
- An alias is resolved by its scope (`XamlStructure.GetByAlias(alias, position)`): an alias
  declared again on a nested element means the nested declaration inside of it. A mapping
  references the moved class only if it names the assembly of the class as well
  (`XamlMove.IsMappedBy`: its `;assembly=`, or the assembly of the xaml when there is none).
- A newly created xmlns keeps the form of the source one (`using:` stays `using:`) and is
  declared on the root element, the only place visible from everywhere. The cleanup removes the
  declarations the move has left unused, and only those.
- The `XamlTypeUsage` scan is a greedy one: it collects everything which looks like an
  `alias:ClassName` pair and is not a part of a fragment recognized above. Such a pair is
  rewritten only if its alias is a CLR-namespace mapping which points to the namespace the class
  is moved out of, so the pairs which reference nothing (`mc:Ignorable="d"`, a time in a text)
  are simply skipped.

## The console utility

`AdjustNamespace.Cli` is the second host of the core. There is no IDE, so it does itself what
Visual Studio does for the extension:

- `MsBuildSolutionLoader` opens the `.sln`, the `.slnx` or the `.csproj` into an
  `MSBuildWorkspace`. `Program` registers `MSBuildLocator` before that and touches no MSBuild
  type itself: a method which mentions one must not be jitted before the locator has found the
  MSBuild of the installed .NET SDK. A `.slnx` is understood by
  `Microsoft.CodeAnalysis.Workspaces.MSBuild` 5.0 and newer, which is why the utility uses a
  newer Roslyn than the extension does. What MSBuild fails to load (a `sqlproj`, a `shproj`)
  is reported as a warning and left out of the run.
- `AdjustCommand` walks the three steps of the wizard without a user: it compiles every project
  and stops if there are errors (`--force` overrules, exactly as the "Next" button of the first
  step does), collects the subject files and runs an `AdjustSession` with a `ConsoleProgress`.
  `--dry-run` and `--check` stop after the collecting; they differ in the exit code only.
  After a real run the Markdown of the `AdjustReport` is printed and, with `--report <path>`,
  written to a file (JSON if the extension is `.json`). `--text <mode>` and `--text-kinds <list>`
  tell how aggressive the search of the text references is (the default is balanced, without the
  comments), over the settings file of the solution; `--dry-run` lists the matches with their
  scores (`--verbose` adds the signals of every score).
- `MsBuildSolutionExplorer` answers out of the loaded workspace. Two things do not come from
  Roslyn: the generated sources under `obj` are dropped (the solution tree of Visual Studio
  never showed them, MSBuild reports them as usual documents of the project) and the xaml files
  are searched for on the disk, in the folders of the projects.
- When a `.csproj` is named instead of a solution, the projects it references are loaded with it
  and are needed — the semantic model is built of them and the references to the moved types are
  fixed in them — but only the files of the named project are candidates to be adjusted.

## Threading

Visual Studio automation objects (DTE, the solution tree, the editor documents) are available
from the main thread only, so such work is grouped into the separate steps which start with
`ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync`. That is the IDE part; the core is
free of it and offloads the heavy Roslyn analysis with a plain `Task.Run`, which means the same
thing in a console process and inside Visual Studio.

## Logging

The detailed diagnostics of the adjusting (`[Adjust] ...` lines: the searched types, the found
references, the scheduled edits, whether a namespace stays alive) go through `AdjustLog`. They
still reach a debugger via `Debug.WriteLine`, and a host may also send them to a file:

- the console utility writes them into `%TEMP%\AdjustNamespace.cli.log` when started with
  `--debug`;
- `Logging.LogVS` of the IDE part writes the exceptions of the commands and of the wizard into
  `%TEMP%\AdjustNamespace.vs.log` and is compiled into the debug builds only
  (`[Conditional("DEBUG")]`).
