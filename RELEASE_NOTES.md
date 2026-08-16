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

## 0.5.1

- Added the support of the arm64 Visual Studio: the extension refused to install there, because its manifest declared the amd64 architecture only.
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
- Explicit support for MAUI and Avalonia xaml: the `.axaml` extension is treated like
  `.xaml`, and `xmlns:…="using:…"` mappings are rewritten in the same form (Avalonia's
  preferred syntax; also used by MAUI). Covered by automated tests; verified by building
  `Tests/Standard/TestMauiApp` (Windows) and a minimal Avalonia desktop app on this machine.
- Automated tests for C# files inside a `sqlproj` (the `RootNamespace` / folder chain and
  the fixed references), matching the sample `Tests/Standard/DatabaseProject`.

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
