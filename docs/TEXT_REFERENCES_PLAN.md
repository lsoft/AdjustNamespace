# Text references, the run report and the wizard UI (0.8.0)

The plan of version 0.8.0. Roslyn sees the references to a type which are written as code;
everything else which names a type by text is out of its sight and keeps the old name after a
move: `Type.GetType("A.B.C, MyAsm")`, `new ResourceManager("A.B.Properties.Resources", ...)`,
`<StartupObject>A.B.Program</StartupObject>`, `@model A.B.ViewModel` of Razor, the `type="..."`
attributes of the config files, comments. Some of them break the build, most of them break the
program at run time only.

The version brings three things:

1. a search of such text references with a **confidence score** of every match;
2. the **wizard UI** for it: an aggressiveness setting and a review step;
3. a **run report**: what has been adjusted and what has not, shown at the end of the run
   instead of closing the window, and printed by the console utility.

## 1. What is searched

Only the full names of the moved types, never a short name (`Customer`, `Models` are far too
common). For every type moved by the session:

- `A.B.C` and `global::A.B.C`;
- the metadata forms: `A.B.Outer+Inner` (nested), ``A.B.C`1`` (generic arity);
- the assembly qualified form `A.B.C, MyAsm` (with optional `Version=...` and the rest);
- the namespace itself (`A.B`) in the places which name a namespace (Razor `@using`,
  `GeneratedClassNamespace`, `CustomToolNamespace`), and only when that namespace is emptied by
  the session or moved as a whole.

A match has name borders: `X.A.B.C` (a dot or an identifier character in front) and
`A.B.CExtra` (an identifier character behind) do not match. A dot behind the name is allowed
(`A.B.C.Member`).

The replacement is the same name with the target namespace, in the same form
(`A.B.C, MyAsm` becomes `X.Y.C, MyAsm`).

## 2. The confidence score

Every match gets a score from 0 to 100: a base score for the place it is found in, plus the
modifiers. The score is clamped into 0..100.

### Base score

| Place | Base |
| --- | --- |
| Razor directive (`@using`, `@model`, `@inherits`, `@inject`, `@typeparam`, `@namespace`), `<StartupObject>`, `GeneratedClassNamespace` of a `.settings` file, `CustomToolNamespace` of a project item | 100 |
| C# string literal passed to a known type name parameter: `Type.GetType`, `Assembly.GetType`, `Activator.CreateInstance(string, string)`, `AppDomain.CreateInstance*`, `[TypeConverter(string)]`, `[Designer(string)]`, `[Editor(string, ...)]`, `[DebuggerTypeProxy(string)]`, `[TypeForwardedFrom]`, the `baseName` of `ResourceManager`; any parameter named `typeName`, `assemblyQualifiedName`, `className`, `baseName` (found through the semantic model) | 95 |
| XML config attribute `type`, `typeName`, `contract`, `factory`, `<service name="...">` (app.config, web.config, NLog, log4net, Unity and similar) | 85 |
| JSON value of `$type`, or of a key matching `type`, `class`, `using` (case insensitive: Serilog `Using`, a DI config) | 80 |
| Any other C# string literal which is the name as a whole | 60 |
| Any other XML or JSON value which is the name as a whole | 50 |
| A comment, the text of a documentation comment (not a `cref`, which Roslyn handles) | 40 |
| The name inside a longer string (a sentence, a message) | 30 |

### Modifiers

| Signal | Modifier |
| --- | --- |
| Assembly qualified, the assembly is the one of the moved type | +20 |
| Assembly qualified, the assembly is **another** one (a type of the same name elsewhere) | -40 |
| Metadata form (`+`, `` `N ``) matching the real nesting or arity | +10 |
| The literal or the value is exactly the name (after trimming) | +10 |
| The name stands in prose (letters or spaces around it, not quotes or `=`) | -10 |
| Looks like a file or a resource: followed by a file extension or `.` + a lowercase letter, or part of a path or a URL | -40 |
| The matched text is a namespace only and that namespace stays alive after the move | excluded |
| The matched text is a namespace only and that namespace is emptied | capped at 89 (never "certain") |
| The name means something else too (a namespace or type of that full name in another referenced assembly) | -30 |
| Test data (a file under a folder named like `TestData`, `Fixtures`) or a value of a `.resx` file | -20 |
| A generated file (`GeneratedCode.IsGeneratedFile`), `bin`, `obj`, a skipped folder | not scanned |

### Levels

| Level | Score |
| --- | --- |
| Certain | 90..100 |
| Likely | 70..89 |
| Possible | 45..69 |
| Unlikely | 0..44 (hidden unless asked for) |

Every match keeps the list of the signals which produced its score; the UI shows it as a
tooltip ("why this score") and the report prints it. The weights above are the starting point:
`Tests` gets a corpus of real positive and negative examples, and every one of them has to land
in its expected level, so a change of a weight is checked against all of them.

## 3. Where it is searched

| Scanner | Files | How |
| --- | --- | --- |
| C# | the documents of the solution | Roslyn syntax: string literal tokens of every kind (regular, verbatim, raw, UTF-8, the text parts of an interpolated string), comment trivia, the text of documentation comments. Exact spans. |
| Razor | `.cshtml`, `.razor` (including `_ViewImports`) | the directive lines; the generic arguments of `@model`, `@inherits`, `@inject` |
| Project | `.csproj` | `<StartupObject>`, the `CustomToolNamespace` metadata |
| Settings | `.settings` | `GeneratedClassNamespace`, the `Type` of a setting |
| XML config | `*.config`, `nlog.config`, `log4net.config`, other `.xml` given by the settings | the attribute values; positions taken from `XmlReader` (`IXmlLineInfo`) |
| JSON | `appsettings*.json`, other `.json` given by the settings | the string values; positions from `Utf8JsonReader` |

The non C# files are found through the existing `ISolutionExplorer` (Visual Studio) and through
the project items plus the project folders (console utility), minus `bin`, `obj` and the skipped
folders of `adjust_namespaces_settings.xml`.

A replacement of a non C# file replaces a span of the original text: the file is never
re-serialized through a DOM, so the formatting, the encoding, the BOM and the line breaks stay as
they are (the same rule the xaml files follow, see `XamlFileWritingTests`). In Visual Studio the
files are changed through the same invisible editor mechanism the xaml files use
(`InvisibleXamlBodyProvider`, to be generalized to any text file), so the global undo of the run
covers them.

The C# matches become `ReplaceTextEdit`s of the session; the other ones a new edit kind of the
same `EditSet`, so the analysis stays separated from the application as it is now.

The scan needs the moved types and their targets only, which the plans of the session know before
anything is changed: it runs before the adjusting and its result feeds the review step.

## 4. Aggressiveness

| Mode | Scan | Applied | Shown unchecked |
| --- | --- | --- | --- |
| Off | no | — | — |
| Report only | yes | nothing | everything from Possible |
| Conservative | yes | Certain | Likely, Possible |
| **Balanced** (default) | yes | Certain, Likely | Possible |
| Aggressive | yes | Certain, Likely, Possible | — |

Per kind switches next to it: comments (off by default), XML configs, JSON, Razor, project and
settings files. The defaults live in Tools → Options; `adjust_namespaces_settings.xml` may
override them for a solution (same file, new elements; an old file without them keeps working).

## 5. The wizard

### Selection step (existing)

A collapsible "Text references" block under the file list: the aggressiveness combo box and the
per kind check boxes. The "Cannot be adjusted" panel stays as it is.

### Review step (new)

Shown between the selection and the run only when the scan has found something to show.

- the matches grouped by file; a row is: a level badge, the line with the match highlighted,
  `old → new`, a check box (preset by the aggressiveness), the tooltip with the signals;
- a filter by level; "check all Likely", "uncheck all";
- a double click opens the file at that line;
- "Next" runs the session with the checked matches only.

### Report step (new, replaces the automatic close)

- a summary line: adjusted N files, blocked M, skipped K, not processed (cancelled) L, text
  references changed X / left Y;
- **Adjusted**: per file, the old namespace → the new one and the number of the edits in the
  other files; expands into the touched files with the kinds of the edits (using added, name
  qualified, `global::` added, `x:Class`, xmlns, `XmlnsDefinition`, `nameof` replaced, using
  removed by the cleanup);
- **Not adjusted**: blocked (with the reason, the same texts as the selection step), skipped
  (nothing to move), not processed because of the cancel;
- **Text references**: changed and left, with the level and the navigation;
- **Build check** (optional, see the options): the compilation errors of the affected projects
  before and after the run, the new ones listed with navigation;
- buttons: "Copy as Markdown", "Save report...", "Open log", "Close".

Whether the step is shown is an option: always (default), only when something was not adjusted
or left, never (the window closes as now).

The items which were not fixed (left text references, new compilation errors) also go to the
**Error List** as warnings, so they stay visible after the window is closed.

### Options page

Today every option is hidden. Visible from 0.8.0: the default aggressiveness, the per kind
switches, when to show the report, whether to run the build check.

## 6. The core: `AdjustReport`

`AdjustSession.RunAsync` returns an `AdjustReport` (a plain data object of
`AdjustNamespace.CoreShared`, no Visual Studio types) instead of the bare `AdjustSessionOutcome`
(which becomes a property of the report):

- the outcome of the session (completed, cancelled);
- per subject file: adjusted / skipped / blocked (with the `AdjustBlock`) / not processed /
  failed (with the exception message), the old and the new namespaces;
- per touched file: the edits by kind (taken from the `EditSet`s of the session and from the
  cleanup);
- the text references: the match, the score, the level, the signals, applied or left;
- the build check, when it was asked for.

The report renders itself as Markdown and as JSON (one renderer each, in the core), so the wizard,
"Copy as Markdown" and the console utility print the very same text.

## 7. The console utility

- `--text off|report|conservative|balanced|aggressive` (default `balanced`);
- `--text-kinds comments,xml,json,razor,project` (default all but `comments`);
- `--report <path>`: writes the report, Markdown or JSON by the extension;
- `--dry-run` lists the text references with their scores as well;
- the exit code stays as documented; a left "Likely" match is not an error.

## 8. Phases

Every phase ends with the whole test suite green, the solution built (`Ошибок: 0`) and the
documentation updated.

1. **`AdjustReport`** in the core: the per file outcomes, the edit summary, the Markdown and JSON
   renderers; the console utility prints it and writes `--report`; the wizard gets the report
   step (the build check comes later). Useful without any text search. **Done in 0.8.0.**
2. **The C# scanner** with the scoring and the levels, the aggressiveness in the core and in the
   console utility, the corpus tests of the score. **Done in 0.8.0.**
3. **The wizard**: the text references block of the selection step, the review step, the options
   page. **Done in 0.8.0.**
4. **Razor, `.csproj`, `.settings`** scanners. **Done in 0.8.0.**
5. **XML and JSON** scanners. **Done in 0.8.0.**
6. **Error List** and the **build check**. **Done in 0.8.0.**

## Implementation notes (what differs from the plan above)

All six phases are done in 0.8.0. Where the implementation differs from the text of the plan:

- **Positions of xml.** The xml scanners use a small tolerant positioned reader
  (`XmlTokenizer`) and not `XmlReader` with `IXmlLineInfo`: that interface gives the line of a
  node, not where the value of an attribute starts. For json a hand written reader is used and
  not `Utf8JsonReader`, because the core is compiled for .NET Framework 4.8 too.
- **Skipped folders.** The files that are no C# documents are *not* filtered by the skipped
  folders of `adjust_namespaces_settings.xml` (only `bin`, `obj`, `node_modules`, `.git` and `.vs`
  are left out): the skipped folders are folders that do not take part in the namespaces, and the
  usual one, `Properties`, holds the `.settings` files.
- **Body providers.** `IXamlBodyProviderFactory` is used for any text file as it is (it already
  is a path -> text reader / writer, and `InvisibleXamlBodyProvider` works for any file) instead
  of a renamed or a second interface.
- **When the text is changed.** The text references are changed *before* the first file is
  adjusted, so that the spans of the search are valid; a match is verified against the text
  right before it is changed and reported as left if the text has changed since the search.
- **The report.** The edits of an adjusted file are listed under that file (the files it has
  changed, with the kinds), the cleanup is a section of its own, and the xaml edits have two kinds
  (`x:Class` and "references and xmlns"). A session that fails on a file stops with
  `AdjustSessionOutcome.Failed` (the report names the file) instead of throwing, and the wizard
  rolls the undo transaction back as it did on an exception.
- **Settings of the setting `Type`.** The `Type` of a setting of a `.settings` file is scored as
  an xml type attribute (85).
- **Build check.** Besides the options page there is `adjustns --build-check`, and the compilation
  errors are the ones of the Roslyn compilations (no build is run).
- **Open log.** The button opens `%TEMP%\AdjustNamespace.vs.log`, which a Debug build writes; it
  is disabled when the file does not exist.

## 9. Not in scope

- Names built at run time (`"A.B." + name`): undetectable, said so in the documentation.
- Serialized data (`$type` in a database, a cache, a queue): cannot be fixed by an edit of the
  sources; a match in a JSON data file adds a note to the report that such data may exist
  elsewhere.
- The `Designer` files of old Entity Framework migrations are history and are not changed; only
  the model snapshot is a subject (as an ordinary C# file with string literals).
- Short names, partial names inside strings (`"B.C"` for `A.B.C`).
