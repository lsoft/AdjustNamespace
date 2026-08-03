# Manual tests

There is no automated test suite for the extension: it modifies the code of a solution opened in
Visual Studio, so it is tested manually against a sample solution which lives here.

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
| `TestSharedProject` | A shared project (`.shproj`): the default namespace is derived from the project name. |
| `DatabaseProject` | C# files inside a `sqlproj`. |
| `TestMauiApp` | MAUI xaml, which uses another uri for the xaml language namespace. |

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

`git status` is useless inside `Tests\Subject` (the folder is ignored), so compare it with
`Tests\Standard` if you need to review what exactly has been changed.

## Adding a new case

Add the repro into the corresponding project of `Tests\Standard` (or create a new project there),
rebuild `AdjustNamespace.2022` and check that the case is processed correctly. Please keep the
repro as small as possible and mention the issue number in the file or folder name if it is
related to a github issue.
