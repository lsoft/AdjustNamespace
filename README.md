# Adjust Namespaces

AdjustNamespace is a Visual Studio 2022 extension which brings the C# namespaces in accordance with the location and **rules the resulting regressions in the code (including XAML), e.g. fixes the broken references**. This extension works like Resharper `Adjust namespaces` function. If you know Resharper, you know what this extension is trying to do.

The same job is done by the console utility `adjustns` (see [Console utility](#console-utility)), which needs no Visual Studio at all and is therefore usable on a build server.

## How to use

Select object (solution, project, folder or file) in solution explorer, click RMB, choose `Adjust Namespaces...` and follow the wizard.

![Usage example](https://raw.githubusercontent.com/lsoft/AdjustNamespace/main/demo1.png)

or choose the whole solution by this way:

![Usage example](https://raw.githubusercontent.com/lsoft/AdjustNamespace/main/demo2.png)

![Usage example](https://raw.githubusercontent.com/lsoft/AdjustNamespace/main/demo3.png)

You can also exclude some folders from participating in namespace chain. AdjustNamepace stores such settings in its configuration xml file, in the folder of your solution. Commit that file to share it across your team.

### Commands

| Command | Where it lives | What it does |
| --- | --- | --- |
| `Adjust namespaces...` | Solution explorer context menu (solution, project, folder, file) | Adjusts the files of the selected items. |
| `Adjust namespaces in solution...` | `Extensions -> Adjust Namespace` | Adjusts every file of the opened solution. |
| `Adjust namespaces in selected...` | `Extensions -> Adjust Namespace` | Same as the context menu command, but available regardless of the active window. |
| `Edit skipped paths...` | `Extensions -> Adjust Namespace` | Edits the folders which must not take a part in the namespaces. |
| `Show release notes...` | `Extensions -> Adjust Namespace` | Opens `RELEASE_NOTES.md` of the installed version. |

### The wizard

1. **Preparation.** Every project of the solution is compiled and the found errors are reported. The adjusting is based on the Roslyn semantic model, so a broken solution may lead to incorrect results. You are allowed to move next anyway.
2. **Selection.** The chosen files are scanned and only those which really have to be changed are shown, grouped by their folder. Here you may uncheck the files you do not want to touch, tune the target namespace with a regex (see below) and decide whether the changed files have to be opened in the editor. If the target namespace of a file already contains a type with the same name, the scan stops with an error: such a move would break the compilation.
3. **Performing.** The files are processed one by one, then the using clauses of the namespaces which became empty are removed from the whole solution.

## How the target namespace is calculated

For every file the target namespace is built as

```
<default namespace of the project> + <folders between the project folder and the file>
```

and then modified by the optional user regex.

- The default namespace is taken from the project properties (C# and `sqlproj` projects). For the other project kinds (a shared project, for example) the project name without its last part is used: `MyApp.Shared` -> `MyApp`.
- The folders excluded by the user (see `Edit skipped paths...`) do not take a part in the resulting name.
- The user regex is applied to the whole resulting namespace. The second step of the wizard offers a few built in samples, e.g. `^[^.]+` with an empty replacement renames the first part of every target namespace.
- Files located outside of their project folder (linked files) are skipped.
- Files compiled by more than one project are skipped: a file of a shared project (`.shproj`) which is referenced by several projects would get a namespace derived from one of them, which does not match the other ones. A shared project referenced by a single project is adjusted as usual, and so is a multi target project (`net48;net8.0`), whose files belong to several projects too but to a single project of the solution.
- A file of a multi target project is skipped when its target frameworks disagree whether the namespace it is moved out of stays alive (a type of that namespace is declared under `#if NET8_0`, for example): the `using` clause of such a namespace is required by one target framework and does not compile for another one, and both of them are built of the same file.

## Settings

The settings are stored in `adjust_namespaces_settings.xml`, in the folder of your solution. Commit that file to share the settings across your team.

```xml
<?xml version="1.0"?>
<AdjustNamespaceSettings xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <SkippedFolderSuffixes>
    <!-- a rooted path -->
    <string>C:\projects\MySolution\MyProject\A\B\C\</string>
    <!-- or a path relative to the folder of the solution -->
    <string>MyProject\A\B</string>
  </SkippedFolderSuffixes>
</AdjustNamespaceSettings>
```

The extension also keeps a few per user options (`Tools -> Options -> Adjust Namespaces`); they are used for the release notes gold bar and for the rating request only.

## What is fixed

C# files:

- the namespace declarations, both the classic (`namespace A { }`) and the file scoped (`namespace A;`) ones;
- the references to the moved types across the whole solution: a new `using` clause is added, or the fully qualified name is rewritten in place;
- the usages of the extension methods declared in the moved static classes;
- the using clauses which point to the namespaces emptied by the adjusting are removed.

XAML files:

- the `x:Class` attribute;
- the tags which reference a moved class through an xmlns alias;
- the `{x:Type}` and `{x:Static}` markup extensions;
- the other places where a class is referenced through an xmlns alias: an attribute value (`TargetType="local:MyButton"`), an attached property (`local:Helper.IsEnabled="True"`), a custom markup extension (`{local:UpperCase}`) and `x:TypeArguments`;
- the `xmlns:...="clr-namespace:..."` clauses: a clause for the target namespace is created when required and the clauses which became unused are removed.

## Undo

By default the files are changed without opening them in the editor, so such changes cannot be undone with Ctrl+Z. Check `Open affected files to enable Undo` on the second step of the wizard to open the affected files instead. The checkbox is disabled if too many files are chosen, because a lot of documents opened at once makes Visual Studio unresponsive.

## Console utility

`adjustns` is the same core without the IDE: it opens a `.sln`, a `.slnx` or a `.csproj` with MSBuild, adjusts the namespaces and writes the files. It needs the .NET SDK installed (the projects are evaluated by the MSBuild of it) and nothing else — no Visual Studio, no extension.

It is not published to NuGet yet, so it is built from the sources:

```
dotnet pack AdjustNamespace.Cli -c Release
dotnet tool install --global --add-source AdjustNamespace.Cli\bin\Release AdjustNamespace.Cli
adjustns MySolution.sln
```

or simply run in place:

```
dotnet run --project AdjustNamespace.Cli -- MySolution.sln --dry-run
```

```
adjustns <solution> [options]
```

| Option | What it does |
| --- | --- |
| `<solution>` | The `.sln`, `.slnx` or `.csproj` to adjust. A folder is accepted too if it contains exactly one of them. |
| `-p`, `--path <path>` | Adjust this file, or the files under this folder, only. May be repeated; the whole solution is taken if it is omitted. |
| `--regex <regex>`, `--replacement <text>` | The same user regex the second step of the wizard offers: it is applied to every target namespace. Both of them are required for the regex to take effect. |
| `-n`, `--dry-run` | Report what would be changed and change nothing. |
| `--check` | Change nothing and exit with the code `2` if there is something to adjust. This is the mode a CI job runs the utility in. |
| `--force` | Adjust even if the solution does not compile. |
| `-v`, `--verbose` | Report every processed file and not the changed ones only. |
| `--debug` | Write the detailed `[Adjust]` diagnostics into `%TEMP%\AdjustNamespace.cli.log`. |
| `-h`, `--help` | Show the help. |

The exit code is `0` when the run is over (including "there was nothing to do"), `1` when the utility has failed and `2` when `--check` has found the files to adjust.

A few things which differ from the extension:

- the solution has to compile, otherwise the run stops: there is no wizard to ask, and the adjusting is based on the semantic model. `--force` says "I know what I am doing";
- the changes cannot be undone with Ctrl+Z — the safety net here is your version control, so commit before the run and review the diff after it;
- the files under `bin` and `obj` are never touched: unlike the solution tree of Visual Studio, MSBuild reports the generated sources as usual files of a project;
- naming a `.csproj` adjusts the files of that project only. The projects it references are loaded as well (the semantic model needs them and the references to the moved types are fixed in them), but their own namespaces are left alone;
- `adjust_namespaces_settings.xml` is read from the folder of the solution, exactly as the extension reads it.

## Remarks

I test it against plain C# code, WPF Xaml, and C# code from `sqlproj`. I encourage you test against your codebase and report bugs (with minimal repro) to https://github.com/lsoft/AdjustNamespace/issues.

A few things which are worth to know:

- make sure your solution is compiled successfully before the adjusting, otherwise the results may be incorrect;
- the `System*` and `Microsoft*` namespaces are never touched;
- the xaml files are processed as a plain text (with a set of regexes) to keep your formatting untouched, so an exotic markup may be missed;
- the whole adjusting is a bunch of separate edits, there is no single `undo` transaction for it.

## Requirements

- Visual Studio 2022 (17.0 - 18.0), amd64 or arm64;
- .NET Framework 4.8 (to build the extension);
- .NET 8 SDK (to build and to run the console utility; a newer SDK is fine too).

The extension is a single `AnyCPU` payload which is installed into both the amd64 and the arm64
Visual Studio, so it is built on either of them and no arm64 machine is needed to produce it.
One thing is worth to know on arm64: the SQL Server Data Tools are not available there, so a
`sqlproj` project is not opened by Visual Studio at all and is therefore never adjusted.

## Building from sources

1. Install Visual Studio 2022 with the `Visual Studio extension development` workload.
2. Open `AdjustNamespace.sln` and build it. The dependencies (Community.VisualStudio.Toolkit, Roslyn, VSSDK build tools) are restored from NuGet.
3. Press F5: an experimental instance of Visual Studio with the extension installed is started.

The solution consists of:

- `AdjustNamespace.CoreShared` — a shared project with the core: everything which knows nothing about Visual Studio;
- `AdjustNamespace.VsixShared` — a shared project with the wizard, the menu commands and the boundary to the IDE;
- `AdjustNamespace.2022` — the VSIX project for Visual Studio 2022 (the manifest, the command table, the resources);
- `AdjustNamespace.Cli` — the console utility (`net8.0`), the core over an `MSBuildWorkspace`;
- `Tests/AdjustNamespace.Tests` — the automated tests of the core (`dotnet test`).

The code which depends on the Visual Studio version is guarded with the `VS2022` conditional compilation symbol.

## Tests

The core is covered by `Tests/AdjustNamespace.Tests` (`dotnet test Tests/AdjustNamespace.Tests/AdjustNamespace.Tests.csproj`); the wizard is tested manually against the sample solution from the `Tests` folder. See [Tests/README.md](Tests/README.md).

## Documentation

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — how the extension is arranged inside.
- [RELEASE_NOTES.md](RELEASE_NOTES.md) — what is new.

## Troubleshooting

Debug builds of the extension write a log into `%TEMP%\AdjustNamespace.vs.log`. The console utility writes the same kind of diagnostics into `%TEMP%\AdjustNamespace.cli.log` when started with `--debug`. Please attach the log (and a minimal repro) to your bug report.
