using AdjustNamespace.Helper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdjustNamespace
{
    public static class FunctionSet
    {
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

            var candidateNamespaces = node
                .DescendantNodesAndSelf()
                .OfType<NamespaceDeclarationSyntax>()
                .Select(nds => nds.GetNamespaceInfo(root))
                .ToList()
                ;

            return candidateNamespaces;
        }

        public static NamespaceInfo GetNamespaceInfo(
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

            return
                new NamespaceInfo(
                    string.Join(".", res),
                    string.Join(".", cloned),
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
