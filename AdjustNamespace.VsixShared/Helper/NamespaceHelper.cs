using AdjustNamespace.Namespace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public static bool TryDetermineTargetNamespace(
            this Project project,
            string documentFilePath,
            out string? targetNamespace
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
                targetNamespace = null;
                return false;
            }

            var suffix = documentFolderPath.Substring(projectFolderPath.Length);
            targetNamespace = GetProjectDefaultNamespace(project) +
                suffix
                    .Replace(Path.DirectorySeparatorChar, '.')
                    .Replace(Path.AltDirectorySeparatorChar, '.')
                    ;

            return true;
        }


        private static string GetProjectDefaultNamespace(Project project)
        {
            if (!string.IsNullOrEmpty(project.DefaultNamespace))
            {
                return project.DefaultNamespace!;
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
