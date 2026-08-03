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
  the manual-test sample solution.

## Testing

There is no automated test suite — the extension rewrites the code of a solution opened in
Visual Studio. The manual procedure lives in [Tests/README.md](Tests/README.md).

## Line endings

[.gitattributes](.gitattributes) declares `* text=auto eol=crlf`: the index stores LF, the
working copy is checked out with CRLF. Keep it that way — write CRLF into the files on disk and
do not "fix" the line endings of a file you are editing, otherwise `git diff` shows whole files
as changed instead of the actual edit. If a diff ever looks like a full-file rewrite, check
`git diff --ignore-all-space` before assuming something went wrong.

## Code style

- The extension targets .NET Framework 4.8; `LangVersion` is `latest` and `Nullable` is
  `enable`, with `nullable;CS8766;CS8767` promoted to errors.
- Version-specific code is guarded with the `VS2022` conditional compilation symbol.
- Almost all of the code lives in the shared project `AdjustNamespace.VsixShared`; new files
  have to be added to `AdjustNamespace.VsixShared.projitems`.
