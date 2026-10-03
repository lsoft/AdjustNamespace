# CLAUDE.md

Notes for Claude Code working in this repository. See [README.md](README.md) for the user
documentation and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the internals.

## Building

**`dotnet build` does not work here** and never will: `AdjustNamespace.2022` is a VSIX project
(`TargetFrameworkVersion` = `v4.8`, project type GUID `{82b43b9b-…}`) which needs the full
MSBuild plus the VSSDK targets shipped with Visual Studio. Do not report "cannot build, only
`dotnet` is available" — use the MSBuild of the installed Visual Studio.

Locate MSBuild through `vswhere` and build the solution (PowerShell, from the repository root):

```bash
$msbuild = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -prerelease -products * -find "MSBuild\**\Bin\MSBuild.exe"; & $msbuild AdjustNamespace.sln -t:Build -restore -p:Configuration=Debug -p:Platform="Any CPU" -v:quiet -nologo -clp:ErrorsOnly`;Summary
```

Notes:

- Do not hardcode the MSBuild path — the installed edition changes. `-prerelease` is required:
  the machine currently has only Visual Studio Insiders (`…\Microsoft Visual Studio\18\Insiders`),
  which `vswhere` hides without that switch.
- Pass `-restore` on the first build after a checkout or after touching the package references;
  it can be dropped afterwards to save time.
- Use `-v:quiet -clp:ErrorsOnly;Summary`. At `-v:minimal` the VSIX packaging step dumps the
  whole content of the `.vsix` (thousands of lines).
- A successful build is `Ошибок: 0` / `0 Error(s)` (the MSBuild output is localized) and a
  refreshed `AdjustNamespace.2022/bin/Debug/AdjustNamespace.vsix`.
- The 4 `MSB3277` warnings about conflicting `StreamJsonRpc` / `Microsoft.VisualStudio.*`
  versions are expected and pre-existing.
- The post-build event recreates `Tests/Subject` from `Tests/Standard`, so a build always resets
  the manual-test sample solution. It relies on `$(SolutionDir)`: build the `.sln`, not the
  `.csproj` alone, otherwise the event fails with `*Undefined*Tests\Subject`.
- An incremental build does not refresh `obj/<Configuration>/extension.vsixmanifest`, so after a
  change of `source.extension.vsixmanifest` (the version, above all) run `-t:Rebuild` and check
  the `Identity` inside the produced `.vsix`. A plain build packed 0.5.1 long after the source
  said 0.6.0.

## Testing

The core is covered by `Tests/AdjustNamespace.Tests` — an SDK style xunit project which imports
the shared project and therefore **is** built and run by the plain `dotnet` CLI (this is the one
exception to the "`dotnet build` does not work here" rule above):

```bash
dotnet test Tests/AdjustNamespace.Tests/AdjustNamespace.Tests.csproj
```

Everything except the tests with the trait `KnownBug` is green; keep it that way. A test which
reveals an error before that error is fixed gets that trait: such tests describe the wanted
behaviour and are red on purpose. Do not "fix" such a test to make it green — either fix the
error in the extension and remove the trait, or leave the test as it is.
`--filter "Category!=KnownBug"` gives a clean run, the full run lists the errors to fix and
[Tests/README.md](Tests/README.md) describes them.

Notes:

- 6 files of the shared project are excluded from the test project (they need the code generated
  by the VSIX project), and their `global using` directives are repeated in `GlobalUsings.cs` —
  keep that file in sync with the header of `AdjustNamespacePackage.cs`.
- `TestSolution.CompilationErrorsAsync()` compiles the whole test solution: a test of the
  adjusting result should assert `Assert.Empty(await solution.CompilationErrorsAsync())` and not
  the produced text only, otherwise a name which is written correctly but resolves to another
  type slips through.
- Everything the extension needs from Visual Studio is behind an interface of
 `AdjustNamespace.VisualStudio` (`ISolutionExplorer`, `IProjectDefaultNamespaceProvider`)
 or `AdjustNamespace.Xaml.BodyProvider` (`IXamlBodyProviderFactory`),
 and `AdjustContext` carries them together with the Roslyn workspace.
  Take the narrowest dependency a class really needs — most of the core needs a `Workspace`
  and nothing else. The wizard itself is covered by the manual procedure only,
  see [Tests/README.md](Tests/README.md).
- The test project references a **newer Roslyn than `AdjustNamespace.2022`** (5.9 against 5.0;
  only that compiler understands the C# 15 preview features, see the note in
  [Tests/README.md](Tests/README.md)), so the shared code has to compile against both versions:
  do not use an API which exists in the newer one only. `Microsoft.VisualStudio.LanguageServices`
  has no 5.x release, so both projects compile its 4.14 against a newer Roslyn, hence the binding
  redirects and the explicit MEF composition in `TestSolution`; the `NU1608` warnings about it
  are expected.

## The console utility

`AdjustNamespace.Cli` (the `adjustns` tool) is an SDK style `net10.0` project and is the second
exception to the "`dotnet build` does not work here" rule:

```bash
dotnet build AdjustNamespace.Cli/AdjustNamespace.Cli.csproj
dotnet run --project AdjustNamespace.Cli -- <solution> --dry-run
```

Notes:

- Do not smoke test the CLI against the whole `Tests/Standard` solution: `TestMauiApp`
  needs the MAUI workload (Windows TFM works with `maui-windows` installed here), and a
  `sqlproj` is not opened by `MSBuildWorkspace`. Build a throwaway solution of two small
  `net8.0` projects instead, run the utility over it and `dotnet build` the result — the
  produced code has to compile.
- `Program` must not touch an MSBuild type before `MSBuildLocator` is registered, otherwise the
  jit resolves the MSBuild assemblies too early and the run dies with a `FileNotFoundException`.
  Everything which mentions one lives in `AdjustCommand` and below.

## Line endings

[.gitattributes](.gitattributes) declares `* text=auto eol=crlf`: the index stores LF, the
working copy is checked out with CRLF. Keep it that way — write CRLF into the files on disk and
do not "fix" the line endings of a file you are editing, otherwise `git diff` shows whole files
as changed instead of the actual edit. If a diff ever looks like a full-file rewrite, check
`git diff --ignore-all-space` before assuming something went wrong.

## Code style

- The extension targets .NET Framework 4.8; `LangVersion` is `latest` and `Nullable` is
  `enable`, with `nullable;CS8766;CS8767` promoted to errors.
- The extension supports Visual Studio 18 only and is compiled against the Roslyn of the
  lowest supported release (5.0.0 for 18.0): Visual Studio binds an older Roslyn reference to
  its own one, never a newer one. There is no version-specific code and no conditional symbol
  for it; `Tests/AdjustNamespace.Tests/Build/ProjectConsistencyTests` fails if the shared code
  uses a symbol the Release build of the extension does not define, or if a Roslyn reference
  is newer than the lowest Visual Studio of the manifest allows.
- Almost all of the code lives in two shared projects: `AdjustNamespace.CoreShared` (the
  Visual Studio independent core, compiled into the extension, the tests **and** the console
  utility) and `AdjustNamespace.VsixShared` (the wizard, the commands, the boundary to the IDE).
  A new file has to be added to the `.projitems` of the one it belongs to.
- The core is compiled against .NET Framework 4.8 **and** .NET 10 at once: it may use neither
  the Visual Studio SDK (`Microsoft.VisualStudio.*`, `EnvDTE`) nor an API which exists in one
  of the two target frameworks only. Whatever it needs from the outside goes through an
  interface — see the boundary table in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
