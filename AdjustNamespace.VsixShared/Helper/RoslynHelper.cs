using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Helper
{
    public static class RoslynHelper
    {
        public static bool IsGlobal(this SyntaxNode node)
        {
            if (node is null)
            {
                throw new System.ArgumentNullException(nameof(node));
            }

            return node.ToString().StartsWith("global::");
        }

        public static T? UpTo<T>(this SyntaxNode node)
            where T : SyntaxNode
        {
            //if (node is T t)
            //{
            //    return t;
            //}

            while (node != null)
            {
                if (!(node.Parent is T))
                {
                    return (T)node;
                }

                node = node.Parent;
            }

            return default;
        }

        //public static string GetTargetNamespaceName(
        //    this NamespaceDeclarationSyntax n,
        //    string root
        //    )
        //{
        //    var nn = n.GetFullNamespaceName(root);

        //    if (nn == root)
        //    {
        //        return root;
        //    }

        //    var suffix = nn.Substring(root.Length);

        //    return targetNamespace + suffix;
        //}


        //public static string GetTargetNamespace(
        //    this INamedTypeSymbol symbol,
        //    string sourceNamespace,
        //    string targetNamespace
        //    )
        //{
        //    if (sourceNamespace == targetNamespace)
        //    {
        //        return targetNamespace;
        //    }

        //    var suffix = symbol.ContainingNamespace.ToDisplayString().Substring(sourceNamespace.Length);

        //    return targetNamespace + suffix;
        //}

        public static int GetDepth(
            this SyntaxNode? node
            )
        {
            var depth = 0;

            while (node != null)
            {
                node = node.Parent;
                depth++;
            }

            return depth;
        }


        public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol @namespace)
        {
            foreach (var type in @namespace.GetTypeMembers())
                foreach (var nestedType in type.GetNestedTypes())
                    yield return nestedType;

            foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
                foreach (var type in nestedNamespace.GetAllTypes())
                    yield return type;
        }


        public static IEnumerable<INamedTypeSymbol> GetNestedTypes(this INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nestedType in type.GetTypeMembers()
                .SelectMany(nestedType => nestedType.GetNestedTypes()))
                yield return nestedType;
        }

    }
}
