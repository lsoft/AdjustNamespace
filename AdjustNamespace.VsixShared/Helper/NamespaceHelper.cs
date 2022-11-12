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
            NamespaceInfo namespaceInfo,
#if VS2022
            out List<BaseNamespaceDeclarationSyntax>? fNamespace
#else
            out List<NamespaceDeclarationSyntax>? fNamespace
#endif
            )
        {
            if (syntaxRoot is null)
            {
                throw new ArgumentNullException(nameof(syntaxRoot));
            }

            if (namespaceInfo is null)
            {
                throw new ArgumentNullException(nameof(namespaceInfo));
            }

            //we need for List of namespac syntax because the following code may exists in single file:
            //namespace a { class a1 {} } namespace a { class a2 {} } namespace a { class a3 {} }
            var foundNamespacesDict = new Dictionary<
                string,
#if VS2022
                List<BaseNamespaceDeclarationSyntax>
#else
                List<NamespaceDeclarationSyntax>
#endif
                >();

            var foundNamespaces = syntaxRoot
                .DescendantNodes()
#if VS2022
                .OfType<BaseNamespaceDeclarationSyntax>()
#else
                .OfType<NamespaceDeclarationSyntax>()
#endif
                .ToList();


            foreach (var foundNamespace in foundNamespaces)
            {
                var nn = foundNamespace.Name.ToString();
                if (!foundNamespacesDict.ContainsKey(nn))
                {
                    foundNamespacesDict[nn] = new();
                }
                foundNamespacesDict[nn].Add(foundNamespace);
            }

            foundNamespacesDict.TryGetValue(namespaceInfo.OriginalName, out fNamespace);

            return fNamespace != null;
        }

        public static bool TryGetTargetNamespace(
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
            targetNamespace = project.DefaultNamespace +
                suffix
                    .Replace(Path.DirectorySeparatorChar, '.')
                    .Replace(Path.AltDirectorySeparatorChar, '.')
                    ;

            return true;
        }


        public static List<NamespaceInfo> GetAllNamespaceInfos(
            this SyntaxNode node,
            string root
            )
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }


            var candidateNamespaces = (
                from dnode in node.DescendantNodesAndSelf()
                let tdnode = dnode as NamespaceDeclarationSyntax
                where tdnode != null
                let ni = tdnode.TryGetNamespaceInfo(root)
                where ni != null
                select ni
                ).ToList();

#if VS2022
            var candidateNamespaces2 = (
                from dnode in node.DescendantNodesAndSelf()
                let fsndnode = dnode as FileScopedNamespaceDeclarationSyntax
                where fsndnode != null
                let ni = fsndnode.TryGetNamespaceInfo(root)
                where ni != null
                select ni
                ).ToList();

            candidateNamespaces.AddRange(candidateNamespaces2);
#endif

            return candidateNamespaces;
        }

#if VS2022

        public static NamespaceInfo? TryGetNamespaceInfo(
            this FileScopedNamespaceDeclarationSyntax n,
            string root
            )
        {
            if (n is null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var originalNamespace = n.Name.ToString();
            var clonedNamespace = root;

            if (originalNamespace == clonedNamespace)
            {
                return null;
            }

            return new NamespaceInfo(
                originalNamespace,
                clonedNamespace,
                true
                );
        }

#endif

        public static NamespaceInfo? TryGetNamespaceInfo(
            this NamespaceDeclarationSyntax n,
            string root
            )
        {
            if (n is null)
            {
                throw new ArgumentNullException(nameof(n));
            }

            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var res = new List<string>();

            SyntaxNode? p = n;
            while (p != null)
            {
                if (p is NamespaceDeclarationSyntax nds)
                {
                    res.Add(nds.Name.ToString());
                }

                p = p.Parent;
            }

            res.Reverse();

            var cloned = new List<string>(res);
            if (!string.IsNullOrEmpty(root))
            {
                cloned[0] = root!;
            }

            var originalNamespace = string.Join(".", res);
            var clonedNamespace = string.Join(".", cloned);

            if (originalNamespace == clonedNamespace)
            {
                return null;
            }

            return new NamespaceInfo(
                originalNamespace,
                clonedNamespace,
                res.Count == 1
                );
        }

        public static Dictionary<string, NamespaceInfo> BuildRenameDict(
            this List<NamespaceInfo> infos
            )
        {
            if (infos is null)
            {
                throw new ArgumentNullException(nameof(infos));
            }

            var namespaceRenameDict = new Dictionary<string, NamespaceInfo>();
            foreach (var info in infos)
            {
                var key = info.OriginalName;
                if (!namespaceRenameDict.ContainsKey(key))
                {
                    namespaceRenameDict[key] = info;
                }
            }

            return namespaceRenameDict;
        }

    }

    public class NamespaceInfo
    {
        public string OriginalName
        {
            get;
        }
        public string ModifiedName
        {
            get;
        }
        public bool IsRoot
        {
            get;
        }

        public NamespaceInfo(
            string originalName,
            string modifiedName,
            bool isRoot
            )
        {
            if (originalName is null)
            {
                throw new ArgumentNullException(nameof(originalName));
            }

            if (modifiedName is null)
            {
                throw new ArgumentNullException(nameof(modifiedName));
            }

            OriginalName = originalName;
            ModifiedName = modifiedName;
            IsRoot = isRoot;
        }

    }

}
