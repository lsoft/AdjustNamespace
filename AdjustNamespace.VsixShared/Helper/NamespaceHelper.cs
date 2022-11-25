using AdjustNamespace.Namespace;
using AdjustNamespace.VsixShared.Settings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AdjustNamespace.Helper
{
    public static class NamespaceHelper
    {
        public static bool TryFindNamespaceNodesFor(
            this SyntaxNode syntaxRoot,
            string namespaceName,
#if VS2022
            out List<BaseNamespaceDeclarationSyntax> result
#else
            out List<NamespaceDeclarationSyntax> result
#endif
            )
        {
            if (syntaxRoot is null)
            {
                throw new ArgumentNullException(nameof(syntaxRoot));
            }

            if (namespaceName is null)
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }

            //we need return a List<> of namespaces syntax because the following code may exists in single file:
            //namespace a { class a1 {} } namespace a { class a2 {} } namespace a { class a3 {} }
            result = new();

            var allFoundNamespaceSyntaxes = syntaxRoot
                .DescendantNodes()
#if VS2022
                .OfType<BaseNamespaceDeclarationSyntax>()
#else
                .OfType<NamespaceDeclarationSyntax>()
#endif
                .ToList();


            foreach (var foundNamespaceSyntax in allFoundNamespaceSyntaxes)
            {
                var fnn = foundNamespaceSyntax.Name.ToString();
                if (fnn == namespaceName)
                {
                    result.Add(foundNamespaceSyntax);
                }
            }

            return result.Count > 0;
        }

        public static async Task<string?> TryDetermineTargetNamespaceAsync(
            this Project project,
            string documentFilePath,
            VsServices vss
            )
        {
            if (project is null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (documentFilePath is null)
            {
                throw new ArgumentNullException(nameof(documentFilePath));
            }

            var projectFolderPath = new FileInfo(project.FilePath).Directory.FullName;
            var documentFolderPath = new FileInfo(documentFilePath).Directory.FullName;

            if (documentFolderPath.Length < projectFolderPath.Length || !documentFolderPath.StartsWith(projectFolderPath))
            {
                return null;
            }

            var names = new List<string>();
            var dir = new DirectoryInfo(documentFolderPath);
            while (dir.FullName != projectFolderPath && dir.FullName.Length > projectFolderPath.Length)
            {
                if (!vss.Settings.IsSkippedFolder(dir.FullName))
                {
                    names.Add(dir.Name);
                }

                dir = dir.Parent;
            }

            names.Reverse();
            names.Insert(0, await GetProjectDefaultNamespaceAsync(vss, documentFilePath, project));

            var targetNamespace = string.Join(".", names);
            return targetNamespace;
        }


        private static async Task<string> GetProjectDefaultNamespaceAsync(
            VsServices vss,
            string documentFilePath,
            Project project
            )
        {
            if (!string.IsNullOrEmpty(project.DefaultNamespace))
            {
                return project.DefaultNamespace!;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (vss.Dte.Solution.TryGetProjectItem(documentFilePath, out var vsp, out var _))
            {
                if (vsp != null)
                {
                    if (vsp.Kind == SolutionHelper.DatabaseProjectKind)
                    {
                        var prop = vsp.Properties;
                        if (prop != null)
                        {
                            var dn = prop.Item("DefaultNamespace");
                            if (dn != null)
                            {
                                return dn.Value.ToString();
                            }
                        }
                    }
                }
            }

            var dotIndex = project.Name.LastIndexOf(".");
            if(dotIndex <= 0)
            {
                return project.Name;
            }

            var result = project.Name.Substring(0, dotIndex);
            return result;
        }
    }
}
