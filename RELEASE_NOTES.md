# Overview

Please report any bugs to the [github repo](https://github.com/lsoft/AdjustNamespace).

## Feedback

Visual Studio extension authors suffers of lack of feedback. Please share your feelings and gratitude. Choose one or few available options:

1. Please [gift a ★★★★★ rating](https://marketplace.visualstudio.com/items?itemName=lsoft.AdjustNamespaceVisualStudioExtension2022) for this VSIX in the VS Marketplace.
2. Gift a ★ to the [github repo](https://github.com/lsoft/AdjustNamespace).
3. If you are enjoying FreeAIr to the enough level to donate, there are many [small cancer patients](https://advitausa.org/au/index.php/donate/) that need your help. Please provide your help them!

## Other my VSIXes may interest you

- A [VSIX](https://marketplace.visualstudio.com/items?itemName=lsoft.FreeAIr) is to provide access to AI for free for everyone who is using Visual Studio with no country-based ban. Local LLMs are supported too.
- [Visual Studio extension](https://marketplace.visualstudio.com/items?itemName=lsoft.MultiLineDebugExpressionEvaluatorInternalName) for quick watch window which allows to debug and edit multilines expressions.
- If you are using plain SQL inside you code base you may want to validate these queries against your DB schema right inside Visual Studio. [ReSequel](https://marketplace.visualstudio.com/items?itemName=lsoft.ReSequel64) does exactly that.
- [This extension](https://marketplace.visualstudio.com/items?itemName=lsoft.RelationalRoslynVisualStudioExtension) puts Roslyn metadata of your project into the in-memory sqlite database and allows to you to execute queries to the database.
- [The faster way](https://marketplace.visualstudio.com/items?itemName=lsoft.StringLocalizer) to add strings to your multilanguage resx files. Just install the extension, select the text and press Alt+J.
- A [Visual Studio extension](https://marketplace.visualstudio.com/items?itemName=lsoft.SyncToAsyncExtension) which creates codelenses allows you to go to sync sibling method for async methods and vice-versa even if sibling method is in different file or code generated.

My others extensions lives [here](https://marketplace.visualstudio.com/publishers/lsoft).

# Adjust namespaces Release Notes

## 0.7.0

- **Visual Studio 2026 (18.x) only.** The extension is compiled against the Roslyn of Visual
  Studio 2026 and is not installed into Visual Studio 2022 anymore; 0.6.0 is the last version
  for it. The extension is called `Adjust C# Namespaces` now, without the year.
- Fixed the file scoped namespaces (`namespace A.B;`), which the published (Release) builds
  of the extension did not adjust at all: the code was compiled under a symbol only the Debug
  build defined.
- Fixed the extension members of C# 14 and C# 15 (an extension property, a static extension
  member, an extension operator and indexer): moving their static class left the callers
  without the using clause, and a moved file lost the access to the ones of its old enclosing
  namespace.
- Fixed a type referenced by a nameless tuple element (`(Cat, int)`, also among the case types
  of a union), which was not fixed when it was moved.
- Fixed the file-local types of C# 11 (`file class Helper`), which were reported as a type name
  conflict although the compiler allows another type of the same name in another file.
- A namespace a project file imports (`<Using Include="A.B" />`) is not emptied anymore: such a
  file is blocked with the reason, instead of breaking the next build.
- Windows Forms: a form chosen together with its `.Designer.cs` (or any partial class split over
  two files) is moved as a whole; the second half was reported as a type name conflict and the
  class was torn apart. A file whose partial class has a part which is not chosen is blocked.
- Fixed `Properties.Settings.Default` and similar names of a child namespace of the old
  enclosing namespace, which were lost when a file left that namespace.
- Fixed the `[assembly: XmlnsDefinition(uri, "A.B")]` attributes of WPF, Avalonia and MAUI
  (including the global xmlns of .NET MAUI 10), which kept pointing to the old namespace.
- Fixed the Avalonia style selectors (`Selector="local|MyButton"`), which were not rewritten.
- Xaml: fixed the single quoted attributes, the prefixes with a hyphen, a CLR namespace as the
  default xmlns of an element, a prefix declared again on a nested element, a new mapping
  declared on a nested element, a namespace of the same name in another assembly, a nested
  type of the same name as another class, a markup extension written as an element without its
  `Extension` suffix, and a MAUI page without `xmlns:x`. The xmlns clauses which were unused
  before the move are not removed anymore.
- Fixed a xaml whose code behind lies in another folder or in the global namespace: the
  `x:Class` follows the class of its code behind and nothing else.
- Global undo: the whole adjusting run is one linked undo transaction, so a single Ctrl+Z
  reverts every touched file, including the files which were never opened in the editor.
  The `Open affected files to enable Undo` checkbox of the wizard is gone with it.
- Explicit support for MAUI and Avalonia xaml: the `.axaml` extension is treated like
  `.xaml`, and `xmlns:…="using:…"` mappings are rewritten in the same form (Avalonia's
  preferred syntax; also used by MAUI). Covered by automated tests; verified by building
  `Tests/Standard/TestMauiApp` (Windows) and a minimal Avalonia desktop app on this machine.
- A file which cannot be adjusted safely (a type name conflict in the target namespace, a
  linked file, a file several projects compile, contradictory target frameworks) does not
  stop the whole run anymore: it is listed with the reason on the second step of the wizard
  (and as an `error:` line by the console utility) and the other files are adjusted as usual.
- Fixed the conflict check which missed the enums and the delegates of the adjusted file.
- Fixed an extension method of the old enclosing namespace called as a member access
  (`value.Twice()`), which was left dangling after the move.
- Fixed a case sensitive folder comparison which treated a file of the project folder as a
  linked one on Windows.
- Fixed two files of one run which could land the same type name into the same namespace.
- Fixed the renaming of a root namespace `A` which also rewrote a nested `namespace A`
  inside another wrapper (`MyApp.A` became `MyApp.MyApp`).
- Fixed the duplicated `using X.Y;` which was added next to an existing `using global::X.Y;`.
- Automated tests for C# files inside a `sqlproj` (the `RootNamespace` / folder chain and
  the fixed references), matching the sample `Tests/Standard/DatabaseProject`.
- The console utility `adjustns` targets .NET 10 and is built over Roslyn 5.9, which knows
  the C# 15 preview features (the unions, the closed classes, the extension indexers).
  It needs the .NET 10 SDK now; the adjusted projects may still target any framework.

## 0.6.0

- Added the console utility `adjustns`: the same adjusting as the extension, over an
  `MSBuildWorkspace`, with no Visual Studio required (usable on a build server).
- Split the Visual Studio independent core into `AdjustNamespace.CoreShared`, so the extension,
  the tests and the console utility share one codebase.
- Fixed the `using` clause of the old namespace of a moved file: types of the *child*
  namespaces (including the generated code behind of a xaml file) were counted as keeping the
  parent alive, so a helper project got `using FreeAIr.UI;` while only
  `FreeAIr.UI.NestedCheckBox` still existed there and the clause did not compile.
- The detailed `[Adjust]` diagnostics of the core go through `AdjustLog` and are written into
  `%TEMP%\AdjustNamespace.cli.log` when the utility is started with `--debug`.

## 0.5.1

- Added the support of the arm64 Visual Studio: the extension refused to install there, because its manifest declared the amd64 architecture only.

## 0.5.0

- Added an automated test suite and fixed a lot of the errors it has found.
- Fixed the XAML references which are written neither in a tag nor in an `{x:Type}` / `{x:Static}` markup extension: an attribute value, an attached property, a custom markup extension, `x:TypeArguments`.
- Fixed the placement of the generated `using` clauses in the files which declare several namespaces.
- Fixed the rewritten names which were resolved to a wrong type.
- Fixed the moving of the types of a file which declares one and the same namespace in several ways.
- The files which more than one project compiles are not adjusted anymore: there is no target namespace which suits all of them.
- Multi target projects: every target framework of a file is taken into account now.
- Added the support of the C# unions (`public union Pet(Cat, Dog);`): a case type which is moved into another namespace was silently left behind and the union stopped compiling.
- Fixed the references an adjusted file itself makes to the types of its old enclosing namespace: such a reference relies on the file being nested inside that namespace and was left dangling once the file was moved out of it.
- Fixed the references written in the documentation comments (`<see cref="Class1"/>`): such a reference was skipped silently, so it kept pointing to the old namespace while the `using` clause it resolved through was removed.
- Added the logging of the adjusting (the searched types, the found references and the scheduled changes) and of the Roslyn version in use.
- Added the documentation of the internals.

## 0.4.0

- Improved usability around target namespace regexes.
- Added built in target namespace regexes.
- Added release notes gold bar.
