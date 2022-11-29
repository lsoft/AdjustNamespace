# Adjust Namespaces

AdjustNamespace is a Visual Studio 2022 extension which brings the C# namespaces in accordance with the location and **rules the resulting regressions in the code (including XAML), e.g. fixes the broken references**. This extension works like Resharper `Adjust namespaces` function. If you know Resharper, you know what this extension is trying to do.

## How to use

Select object (solution, project, folder or file) in solution explorer, click RMB, choose `Adjust Namespaces...` and follow the wizard.

![Usage example](https://raw.githubusercontent.com/lsoft/AdjustNamespace/main/demo1.png)

or choose the whole solution by this way:

![Usage example](https://raw.githubusercontent.com/lsoft/AdjustNamespace/main/demo2.png)

![Usage example](https://raw.githubusercontent.com/lsoft/AdjustNamespace/main/demo3.png)

You can also exclude some folders from participating in namespace chain. AdjustNamepace stores such settings in its configuration xml file, in the folder of your solution. Commit that file to share it across your team.

## Remarks

I do not test it against:

0. WinForms applications
0. Web applications (aspx, for example)
0. Any other projects except plain C#, XAML and sqlproj.

I encourage you to do that testing and report bugs (with minimal repro) to https://github.com/lsoft/AdjustNamespace/issues.
