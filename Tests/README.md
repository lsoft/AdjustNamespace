# Tests

The extension is covered on two levels:

- **`AdjustNamespace.Tests`** — the automated tests of the core, see below;
- **the sample solution** (`Standard` / `Subject`) — the manual end to end run inside a real
  Visual Studio, see [Manual tests](#manual-tests).

# Automated tests

`AdjustNamespace.Tests` is an ordinary SDK style project which imports the shared projects
`AdjustNamespace.CoreShared` and `AdjustNamespace.VsixShared`, so it works with the very same
code as the VSIX (and, for the core, as the console utility). It is built and run with the
plain `dotnet` CLI, the full MSBuild and the VSSDK are not required here:

```bash
dotnet test Tests/AdjustNamespace.Tests/AdjustNamespace.Tests.csproj
```

The tests run without Visual Studio at all:

- the xaml subsystem works with a plain string through `MemoryXamlBodyProvider`;
- the core (`CsAdjuster`, the appliers, `Cleanup`) works over an `AdhocWorkspace` built by
  `TestSolution`, and everything the extension needs from Visual Studio is behind an interface
  (`ISolutionExplorer`, `IProjectDefaultNamespaceProvider`,
  `IXamlBodyProviderFactory`) with a fake of
  `Infrastructure` behind it, bound together by `TestSolution.Context`. There is no half built
  service object with `null` fields anymore, so a test which reaches for the IDE gets an answer
  instead of a `NullReferenceException`. The window of the wizard itself is still covered by
  the manual test only — its steps need WPF and the main thread of Visual Studio. What the
  chain of the steps hands over is checked by the compiler instead: `IStepFactory<TParameters>`
  names the parameters of every step, so a wrong wiring is a build error and not something
  a test would have to catch.

Whether a file is a subject to change at all is decided by `AdjustPlanner`, which both steps
of the wizard ask, so that decision is covered here as one thing and not once per caller:
`AdjustRunner` plans a file exactly as the wizard does it before it creates an adjuster.

What has to be written into the files is decided the same way: the analysis fills an `EditSet`
and `EditApplier` writes it afterwards. A test may therefore read the decision itself instead
of the text it produces — which reference gives a new `using` clause and which one is rewritten
in place is a question about an `EditSet` and needs no adjusting at all.

A whole run over the chosen files is `AdjustSession` and no longer a part of the wizard, so
what used to be observable in a running Visual Studio only — the order of the stages, the
namespace state shared by all the files of one run, the progress and the cancellation — is
covered here as well. `AdjustRunner` drives the steps of a session one by one instead and lets
a test name the target namespace of a file explicitly.

The rule which derives the target namespace from the location of a file is a computation over
the paths and needs no Visual Studio at all, so it lives in `TargetNamespaceCalculator` and is
covered here. The single step of it which really asks Visual Studio — the default namespace of
the project — is behind `IProjectDefaultNamespaceProvider` and stays a subject of the manual
test; the fallback for a project Visual Studio reports nothing about is covered here as well.

Everything is green, see [known bugs](#known-bugs). What is covered:

| Area | Tests |
| --- | --- |
| The whole pipeline over a solution | `Adjusting\CsAdjusterTests`, `CsAdjusterReferenceTests` (every kind of a reference: qualified names, `global::`, generics, base types, type constraints, attributes, static members, alias and static usings, xaml, plus the adjusted file's own unqualified references to a type of its old enclosing namespace, see `SelfReferenceFixer`), `CsAdjusterSessionTests` (several files in one session, the cleanup, the repeated runs) |
| The partially qualified names | `Adjusting\CsAdjusterPartialNameTests` (`B.Class1` resolved through the own namespace or through an alias, `typeof`/`nameof`) |
| The member access expressions | `Adjusting\CsAdjusterMemberAccessTests` (a static member of a generic, of a nested and of a static class) |
| The using clauses | `Adjusting\UsingPlacementTests` (a file without any using, a header, a region, a `global using`), `Adjusting\CleanupTests` (when an old using has to disappear and when it must not) |
| The kinds of the declarations | `Adjusting\CsAdjusterTypeKindTests` (a record, a struct, a static and a generic class, the contradicting namespace declarations) |
| The unions | `Adjusting\CsAdjusterUnionTests` (the union itself, its case types — a bare, a qualified, a `global::` qualified, a generic and a nullable one — a generic, a nested and a partial union, a union as a case of another union, a union which implements an interface, a static member of a union, the conflict check, the implicit union conversions, a case type in a union pattern, a hand-written `[Union]` struct, a tuple among the case types), see [A note about the unions](#a-note-about-the-unions) |
| The tuple types | `Adjusting\CsAdjusterTupleTests` (a named and a nameless tuple element) |
| The extension members of C# 14 / 15 | `Adjusting\CsAdjusterExtensionMemberTests` (a classic extension method, an extension block method, property, static member, operator and indexer of a moved class, a moved receiver type, a generic block, the adjusted file's own use of the extension members of its old enclosing namespace) |
| The other C# 10 - 15 syntax | `Adjusting\CsAdjusterModernSyntaxTests` (`global using` of an emptied and of a living namespace, the `<Using>` items of a project file, a lambda return type, a record struct, a generic attribute, a file-local type, a primary constructor with its base call, an alias of any type, an unbound generic in `nameof`, a closed class) |
| WPF xaml | `Adjusting\WpfXamlTests` (application and resource dictionary classes, design instances, object data providers, component resource keys, relative sources, generic themes, generic roots, attached properties in setters, triggers and paths, nested types, `XmlnsDefinition` of an emptied and of a living namespace, single quotes, a default clr xmlns, a prefix declared again in a nested scope, the scope of a new mapping, the mappings unused before the move, a xaml and its code behind in different folders) |
| Avalonia xaml | `Adjusting\AvaloniaXamlCoverageTests` (compiled binding data types, `Design.DataContext`, control themes, `$parent[...]`, selectors, view locators, resource uris, generic tags, nested types, `XmlnsDefinition`, a namespace of another assembly, a view of the global namespace) |
| MAUI xaml | `Adjusting\MauiXamlCoverageTests` (the 2009 xaml uri, the implicit `x` prefix, compiled bindings, Shell content templates, `x:Arguments` / `x:FactoryMethod`, `OnPlatform` type arguments, triggers, behaviors, resource dictionaries, platform heads, source generated parts, the global xmlns, prefixes with a hyphen) |
| Windows Forms | `Adjusting\WinFormsTests` (a form or a component with its designer, only one half of a form chosen, the designer's full names, visual inheritance, design time attributes, binding sources, resources and settings classes, `Properties.Settings` of a form which leaves its namespace, `ApplicationConfiguration`, resx files) |
| The project files themselves | `Build\ProjectConsistencyTests` (the conditional symbols of the shared code against the Release build of the extension, the Debug and Release symbols, the Roslyn references against the lowest Visual Studio of the manifest) |
| The xaml files | `Xaml\XamlDocumentTests` (parsing and moving, including MAUI uris and Avalonia `using:` xmlns), `XamlReferenceKindTests` (the references outside of a tag and of a markup extension), `XamlFileWritingTests` (the encoding and the line endings of the written file), `Adjusting\XamlAdjusterTests` (the `x:Class` of the document itself), `Adjusting\MauiXamlTests`, `Adjusting\AvaloniaXamlTests` (`.axaml` + `using:`), `Adjusting\GeneratedCodeBehindTests` (the generated part of a xaml class does not keep its old namespace alive) |
| C# inside a `sqlproj` | `Adjusting\SqlProjAdjusterTests` (root namespace + folder chain, references between the files) |
| The shadowed names | `Adjusting\CsAdjusterNamespaceNameCollisionTests` (the target namespace ends with the name of the moved type, so a using clause is not enough and the reference is qualified) |
| The shared projects | `Adjusting\SharedProjectTests` (one file compiled by several projects: the ambiguous target namespace of a C# and of a xaml file, the references and the using clauses of every project, the file list of the solution, a namespace which several projects fill) |
| The multi target projects | `Adjusting\MultiTargetTests` (one file compiled by every target framework: the references and the using clauses, a file of a single target framework, xaml, the conditional compilation, a shared project referenced by a multi target one) |
| The namespaces | `Namespace\NamespaceTransitionContainerTests`, `NamespaceNodeSearchTests`, `NamespaceCenterTests`, `NamespaceHelperTests` |
| The target namespace of a file | `Namespace\TargetNamespaceCalculatorTests` (the folder chain, the skipped folders, a file outside of the project folder, the regex, the fallback of the default namespace) |
| The decision what to write into the files | `Edit\EditSetTests` (the grouping by file, the duplicates), `Edit\RefProcessorTests` (which edit a reference gives, and that the analysis itself changes nothing) |
| Writing the decision into the solution | `Edit\EditApplierTests` (the placement of a new using clause, an existing and an alias one, the order of the kinds inside a file, the intersecting replacements, the renaming of a namespace and the using clause of its old name) |
| What is asked of Roslyn | `Roslyn\SyntaxExtensionsTests` (the walk over a syntax tree and the placement of a using clause), `ScopeTests` (which projects and documents may be processed at all) |
| The settings | `Settings\SkippedFolderTests`, `NamespaceReplaceRegexTests` |
| The name conflicts | `TypeContainerTests` |
| The decision what to do with a file | `Adjusting\AdjustPlannerTests` (the transitions of the plan, every `AdjustBlockKind` — no project, unknown target namespace, not a processable document, shared / multi-project, contradictory TFM namespaces, xaml code-behind in several projects — blocks vs silent none, the regex, the single walk through the solution tree) |
| The file list of the wizard | `Adjusting\SubjectFileCollectorTests` (which files are offered, which are blocked with a reason — type conflicts, linked file / unknown target, shared projects, contradictory TFM, no project — and that adjusting only the collected files leaves a compiling solution compiling) |
| A whole run over the chosen files | `Adjusting\AdjustSessionTests` (the stages and their order, a skipped file, the progress reports, the cancellation and the changes it leaves behind, the single walk through the solution tree) |

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
| A file being adjusted may itself reference another type without a `using` clause, relying on being nested inside that type's namespace (`Some.A.B` sees `Some.A` unqualified). `CsAdjuster` only fixed references *to* the types declared in the adjusted file, never the adjusted file's own unqualified references to types of its old enclosing namespace, so moving it out of that namespace left a dangling reference (`CS0246`). Found while adjusting a real solution: a file moved from `NLOutline.Tree.Builder` into `Nlo.NLOutline.Tree.Builder` (a sibling namespace, not nested under `NLOutline.Tree` anymore) lost its unqualified use of `OutlineNode`, declared in `NLOutline.Tree`. | `SelfReferenceFixer` walks the simple names of the adjusted file itself and, for every one that resolves to a type outside the file whose namespace stops being an enclosing one after the move, schedules a `using` clause for it. |
| A rewritten name is a relative one: `X.Y.Class1` written inside the namespace `Some.X` resolves to `Some.X.Y.Class1` and does not compile. | `RefProcessor.IsGlobalPrefixRequired` asks the semantic model whether the first part of the target namespace is shadowed at that position and prefixes the name with `global::` if it is. |
| A new using clause is added behind the last using of the file, and the using clauses inside a namespace declaration are visible in that namespace only: a file with several namespaces gets the clause into the wrong one. | `AddUsingApplier` looks at the using clauses of the compilation unit only and writes the new one among them: such a clause is visible in every namespace of the file and its name is resolved from the root namespace. |
| `namespace A { namespace B { } }` plus `namespace A.B { }` in one file produce two different transitions of `A.B`, and the references are fixed with the wrong one of them. | A type is moved by the transition of the declaration it is written in (`NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf`) and not by a lookup of its namespace name. |
| A file which lies outside of the folder of its project has no target namespace, but the folders were compared as plain strings: `c:\sln\MyApp.Tests\Sub` starts with `c:\sln\MyApp`, so a linked file of a sibling folder whose name merely begins with the name of the project folder got the whole sibling folder into its namespace (`MyApp.MyApp.Tests.Sub`). | `TargetNamespaceCalculator.IsSameFolderOrBelow` stops the comparison at the folder border: the character behind the root folder has to be a separator. |
| A xaml class is referenced not only by a tag and by an `{x:Type}`/`{x:Static}` markup extension, but also by an attribute value (`TargetType="local:MyButton"`), by an attached property, by a custom markup extension and by `x:TypeArguments`. Such a reference is not moved and keeps pointing to the old namespace. | Everything which looks like an `alias:ClassName` pair and is not a part of an already recognized fragment becomes a `XamlTypeUsage`. A pair is rewritten only if its alias is a clr-namespace one which points to the namespace the class is moved out of, so the pairs which are no type references at all (`mc:Ignorable="d"`) cost nothing. |
| A reference written in a documentation comment (`<see cref="Class1"/>`) was skipped silently: such a comment is a trivia of the declaration it is attached to, and `SyntaxNode.FindNode` does not descend into the trivia unless it is asked to, so the node found for the reference was that declaration and no symbol of it matched. The cref kept pointing to the old namespace, and the using clause it resolved through was removed by the cleanup (`CS1574` in a project which generates the documentation file). | `RefProcessor.ProcessLocationAsync` calls `FindNode` with `findInsideTrivia: true`, and `ProcessQualifiedCref` rewrites the container of a cref which spells the namespace out (`<see cref="A.B.Class1"/>`): a cref is a `QualifiedCrefSyntax` and not a qualified name, so the namespace part of it is a separate node. |
| A file moved out of a namespace got a `using` of that namespace because a *child* namespace still had types (another file of the same project, a referenced project, or a stale xaml `.g.cs`). `using Parent;` does not import the children, and once they had moved away too the clause pointed at a namespace the project could not see (`CS0234`). Found while adjusting FreeAIr: `NullToUnsetValueConverter` left `FreeAIr.UI` for `WpfHelpers.NestedCheckBox` and kept `using FreeAIr.UI;` because of `FreeAIr.UI.NestedCheckBox` in the generated code behind. | `IsNamespaceFilledOutside` and the cleanup look at the types declared *directly* in the namespace only (`GetDirectTypes`), not at the nested namespaces. |
| An enum or a delegate of the adjusted file was not checked for a name conflict in the target namespace: `CheckForTypeNameConflictsAsync` walked `TypeDeclarationSyntax` only, while `CsAdjuster` also moves `EnumDeclarationSyntax` and `DelegateDeclarationSyntax`. Moving such a type onto an existing type of the same name broke the solution (CS0101) with no warning on the second wizard step. | The conflict check enumerates the same three kinds of declarations `CsAdjuster` moves. |
| An extension method of the old enclosing namespace, called as a member access (`value.Twice()`), was left untouched by `SelfReferenceFixer`: the name looked "already qualified", and only `INamedTypeSymbol` references were considered. Moving the caller out of that namespace left a dangling call (CS1061). | `SelfReferenceFixer` also schedules a `using` for an extension method invoked as a member access whose declaring type's namespace stops being an enclosing one. |
| A linked-file check compared folders with a case-sensitive `StartsWith`: on Windows, Roslyn and the IDE may hand the same folder over in a different case, and a file inside the project folder was treated as outside of it. | `TargetNamespaceCalculator.IsSameFolderOrBelow` (and the walk up to the project folder) uses `OrdinalIgnoreCase`. |
| Two subject files could both pass the conflict check against the current solution and still land the same type name into the same target namespace (CS0101), because the types about to move were never reserved. | After a file passes the check, `SubjectFileCollector` reserves its moving types in `NamespaceTypeContainer` for the rest of the scan. A type-name conflict (or any other unsafe case) is an `AdjustBlock` for that file only: the scan continues and the other adjustable files stay available; the wizard and the CLI show the reason. |
| Renaming a root namespace `A` also rewrote a nested `namespace A` inside another wrapper (`MyApp.A` became `MyApp.MyApp`): `TryFindNamespaceNodesFor` matched the written name instead of the full one, against its own API docs. | The search compares the full name of every declaration (including enclosing namespaces). |
| An existing `using global::X.Y;` did not suppress adding `using X.Y;` (CS0105): `AddUsingApplier` compared the raw written name, while cleanup already normalized `global::` and whitespace. | Both `AddUsingApplier` and `MoveNamespaceApplier` use `NamespaceHelper.NormalizeUsingName`. |
| A type referenced by a *nameless tuple element* (`(Cat, int) Pair()`, or a tuple among the case types of a union: `union Pet((Cat, int), Dog)`) was not fixed when it was moved: the span of the reference is the span of the whole element, and `SyntaxNode.FindNode` answers the outermost node of such a tie. The cleanup removed the old `using` and the file stopped compiling (CS0246). | `RefProcessor.ProcessLocationAsync` descends from a `TupleElementSyntax` without an identifier to its type, as it does for a nameless `ParameterSyntax`. |
| A moved static class with the *extension members* of C# 14 (an extension property, a static extension member, an extension operator) or C# 15 (an extension indexer) left its callers without the using clause: such a call writes neither the class nor its namespace (`cat.Loud`, `Cat.Create()`, `a + b`, `cat[0]`), and `RefProcessor.FindReferencesForAsync` queried only the members which are `IsExtensionMethod`. An instance method of a block worked by chance: it has a classic extension method behind it. | Every member of every extension block of a moved static class is queried as well. The use of an indexer is reported as an empty span in front of its argument list, and `RefProcessor` goes up from that list to the element access. |
| The adjusted file itself used an extension property, a static extension member, an extension operator or an indexer of its old enclosing namespace: `SelfReferenceFixer` recognized a type and a classic extension method only, so no using clause was added and the file stopped compiling once it left the namespace. | `SelfReferenceFixer` asks `SymbolExtensions.TryGetImportedExtensionContainer` for the static class behind any member, and walks the operator and indexer uses besides the simple names. |
| A *file-local type* of C# 11 (`file class Helper`) was counted by the conflict check as any other type, so a file was blocked from a namespace which has a type of the same name in another file, although the compiler allows it. | `NamespaceTypeContainer.Add` and `SubjectFileCollector` skip the file-local types. |
| A namespace imported by a project file (`<Using Include="A.B" />`, which becomes a `global using` of a generated `obj\...\*.GlobalUsings.g.cs`) could be emptied by the move. The cleanup removed the clause from the generated file: the solution compiled right after the run and broke (CS0246) on the next build, when the file was generated again out of the project file. | Such a file is blocked (`AdjustBlockKind.NamespaceImportedByProjectFile`): by `AdjustPlanner` when the file alone empties the namespace, by `SubjectFileCollector` when the collected files empty it only together. |
| The file scoped namespaces (`namespace A.B;`) were adjusted under `#if VS2022` only, which the tests and the Debug build of the extension defined but the Release build did not: the published extension left such files as they were. No test noticed it, because the tests are compiled with their own project file. | The `VS2022` symbol is gone together with the support of Visual Studio 2022. `Build\ProjectConsistencyTests` checks that every conditional symbol of the shared code is defined by the Release build of the extension and that the Debug and Release builds define the same symbols. |
| The manifest declared Visual Studio 17.0 as the lowest one, while the extension was compiled against the Roslyn of 17.4: Visual Studio binds an older Roslyn reference to its own one, never a newer one, so 17.0 - 17.3 could not load the extension. | `Build\ProjectConsistencyTests` checks every Roslyn reference of the extension against the Roslyn of the lowest Visual Studio of the manifest. |
| A *partial class split over two chosen files* (a form and its `.Designer.cs`, but any partial class): `SubjectFileCollector` reserved the types of the first file in the target namespace and reported the second half as a `TypeNameConflict`, so only one half moved and the solution broke. Choosing only one half was not detected either. Found by the Windows Forms audit (`WinFormsTests`). | A reservation remembers the type which has made it, and the other part of the same type is no conflict. `SubjectFileCollector` blocks a file (`AdjustBlockKind.PartialTypeSplit`) whose partial type has a part which is not collected or would land in another namespace; a part the build writes again (`GeneratedCode.IsWrittenByTheBuild`: the code behind of a xaml, not a `.designer.cs`) does not count. |
| A *partially qualified name through a child namespace* of the old enclosing namespace (`Properties.Settings.Default` written inside `Legacy.Forms`) lost its meaning when the file left that namespace: a using clause does not import child namespaces. | `SelfReferenceFixer` writes the head of such a name out (`Legacy.Properties.Settings.Default`). |
| *Avalonia selectors* (`Selector="local\|MyButton"`, `:is(local\|X)`, `/template/`) were not rewritten, and the cleanup removed an xmlns used by a selector only. | `XamlDocument` reads the `alias\|Name` pairs of the `Selector` attributes as `XamlTypeUsage` with the `\|` separator, and counts them as uses of the alias. |
| A *nested type of the same simple name* as an unrelated top-level type: `CsAdjuster` passed every type to the xaml engine, so `<local:Item/>` of the top-level `Item` followed the nested `Outer.Item`. | The nested types are not passed: xaml names them through the outer type (`local:Outer+Inner`), which moves itself. |
| A *new xmlns was declared on a nested element* when the last xmlns of the document belonged to one, and the root level tags used an undeclared prefix. | A new declaration is written into the start tag of the root element. |
| *Single quoted* `xmlns` and `x:Class`, a *prefix with a hyphen* (`xmlns:my-ctrl`), a CLR namespace as the *default xmlns* of an element (`xmlns="clr-namespace:A.B"`) and a prefix *declared again in a nested scope* were not recognized: the regexes knew double quotes and `\w` prefixes only, and an alias was resolved by its first declaration. | `XamlDocument` reads every declaration with the element it is written on and resolves an alias by its scope (`XamlStructure.GetByAlias(alias, position)`). A default CLR declaration which maps the moved class only follows it, otherwise the tags of the class get a prefix. |
| `[assembly: XmlnsDefinition(uri, "A.B")]` kept the old namespace, which is a string for Roslyn: the xaml which uses that uri (including the global xmlns of .NET MAUI 10) stopped resolving at run time. | `XmlnsDefinitionFixer` replaces the string when the old namespace is emptied, and adds a copy of the attribute for the new namespace when the old one stays alive. |
| A class of the *same namespace in another assembly* (`clr-namespace:Old;assembly=External`) was redirected too. | `XamlMove.IsMappedBy` compares the assembly of a mapping (its `;assembly=`, or the assembly of the xaml) with the assembly of the moved class. |
| The cleanup removed every unused clr-namespace xmlns, also the ones unused *before* the move (the `xmlns:local` of the MAUI template `App.xaml`). | Only the declarations which were used before the move and are unused after it are removed. |
| A xaml and its code behind in *different folders* got the namespaces of their own folders, and a view whose code behind is in the *global namespace* got an `x:Class` of a class which does not exist. | The `x:Class` belongs to the class it names: when that class is declared in a C# file, `AdjustPlanner` plans nothing for the xaml (the `x:Class` follows the class when the C# file moves), and a class of the global namespace is never moved. |
| A markup extension written as an *element without the `Extension` suffix* (`<local:UpperCase/>`) was not followed. | A tag matches the class with the suffix as well, unless the namespace has a class of the shorter name too (`XamlMove.AllowsShortExtensionName`). |
| MAUI: a page with the *implicit `x` prefix* (no `xmlns:x`, the implicit xmlns of .NET MAUI 10) kept its old `x:Class`. | A document which does not declare `x` at all uses `x` as the xaml language prefix. |
| MAUI: the partial class part of a *source generator* (`...\MainPage.xaml.sg.cs`) kept the old namespace alive. | `GeneratedCode` knows the `.sg.cs` suffix. The path of a real generator document is modelled by the test, not verified. |

### A note about the shared projects

A file which more than one project compiles is left as it is: whatever namespace is chosen for
it, it is derived from one of these projects and does not match the other ones. Two cases which
look the same in Roslyn (one file, several documents) are kept apart by
`WorkspaceExtensions.IsCompiledBySeveralProjects`:

- a **shared project** referenced by several projects — the file belongs to several projects
  with **different project files**, there is no target namespace and the file is skipped;
- a **multi target project** (`net48;net8.0`) — Roslyn creates a project per target framework,
  so the file belongs to several projects with the **same project file**. Such a file is
  adjusted as usual, see `A_file_of_a_multi_target_project_is_adjusted`.

A shared project referenced by a single project is not ambiguous either and is adjusted, see
`A_file_of_a_shared_project_of_a_single_project_is_adjusted`. The fallback of
`TargetNamespaceCalculator.DefaultNamespaceFallback` (the default namespace of a project of an
unknown kind is its name without the last part, `MyApp.Shared` -> `MyApp`) serves exactly that
case now.

### A note about the multi target projects

The projects of the target frameworks of a multi target project are not copies of each other:
every one of them defines its own conditional compilation symbols and may compile its own files
(`<Compile Condition="'$(TargetFramework)'=='net48'" />`). A file of such a project has one text
and a syntax tree per target framework, so everything which reads it reads all of its documents
(`WorkspaceExtensions.GetDocuments`) and everything which writes it addresses a span of the text and
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

### A note about the unions

The [unions](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md) of C#
(`public union Pet(Cat, Dog);`) need a compiler which understands them, so
`AdjustNamespace.Tests` references a **newer Roslyn than `AdjustNamespace.2022` does**
(5.9 against 5.0, see the comments in the `.csproj`): the extension takes Roslyn from the
Visual Studio it runs in and is compiled against the Roslyn of the lowest one it supports, and
the test project brings its own. The consequence is that the shared code has to compile
against **both** of them (and the core, additionally, against .NET 10 — see
`AdjustNamespace.Cli`) — do not use an API which
only the newer version has (`SyntaxKind.UnionDeclaration`, for example, does not exist in
5.0 and is `[Experimental]` in 5.9).

A test which declares a union asks `TestSolution.WithUnionSupport()` before it adds the projects.
That switches the projects to the preview features of the language and gives every one of them
the runtime types a union is lowered to (`IUnion`, `UnionAttribute`; `IsExternalInit` comes with
them, because a case type is usually a positional record). These types belong to
`System.Runtime.CompilerServices`, which is a special namespace for the extension, so the file
of them is never adjusted.

For the syntax tree a union is a `StructDeclarationSyntax` of the kind `UnionDeclaration`, i.e.
an ordinary `TypeDeclarationSyntax`: everything which enumerates the declarations of a file sees
it without a change. What is new is the **case list**: it is a `ParameterListSyntax` whose
parameters consist of a type and have no name at all, and this is the only place where the span
of a type reference is the span of a whole parameter. `RefProcessor` descends from such
a parameter to its type, exactly as it does for a type constraint or for a base type.

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
| `DatabaseProject` | C# files inside a `sqlproj` (also covered by `SqlProjAdjusterTests`). |
| `TestMauiApp` | MAUI xaml (2009 xaml language uri). Builds on a machine with the `maui-windows` workload for the Windows TFM. |

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

The chrome of the wizard has no automated test at all, so walk it through at least once per
change of it: `Cancel` on the first and on the second step closes the window, `Back` on the
second step returns to the first one with the same files, and `Cancel` during the adjusting
stops it and leaves the already adjusted files as they are.

`git status` is useless inside `Tests\Subject` (the folder is ignored), so compare it with
`Tests\Standard` if you need to review what exactly has been changed.

## Adding a new case

Add the repro into the corresponding project of `Tests\Standard` (or create a new project there),
rebuild `AdjustNamespace.2022` and check that the case is processed correctly. Please keep the
repro as small as possible and mention the issue number in the file or folder name if it is
related to a github issue.
