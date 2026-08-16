//The shared core has no global usings of its own: they are declared in AdjustNamespacePackage.cs,
//which belongs to the extension and is not a part of this project. The two the core really uses
//are repeated here (the alias hides the Task of the Visual Studio shell inside the extension and
//changes nothing here, but the core is written with it).
global using System;
global using Task = System.Threading.Tasks.Task;
