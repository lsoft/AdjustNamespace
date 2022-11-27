using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Helper
{
    public static class RoslynHelper
    {
        public static QualifiedNameSyntax? ToUpperSymbol(
            this QualifiedNameSyntax qns,
            SemanticModel semanticModel
            )
        {
            while (true)
            {
                var symbol = semanticModel.GetSymbolInfo(qns.Left).Symbol;
                if (symbol == null)
                {
                    return null;
                }
                if (symbol.Kind != SymbolKind.NamedType)
                {
                    return qns;
                }

                var pqns = qns.Left as QualifiedNameSyntax;
                if (pqns == null)
                {
                    return null;
                }

                qns = pqns;
            }

        }

        public static bool IsGlobal(this SyntaxNode node)
        {
            if (node is null)
            {
                throw new System.ArgumentNullException(nameof(node));
            }

            return node.ToString().StartsWith("global::");
        }

        public static T? ToUpperSyntax<T>(this SyntaxNode node)
            where T : SyntaxNode
        {
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

        public static SyntaxNode? GoDownTo(this SyntaxNode node, Type targetType)
        {
            if (node.GetType() == targetType)
            {
                return node;
            }

            var toProcess = node.ChildNodes().ToList();

            while (toProcess.Count > 0)
            {
                //check any children to match
                foreach (var child in toProcess)
                {
                    if (child.GetType() == targetType)
                    {
                        return child;
                    }
                }

                //not found in direct children
                //get children from next level of depth
                var toProcess2 = new List<SyntaxNode>();
                foreach (var child in toProcess)
                {
                    toProcess2.AddRange(child.ChildNodes());
                }

                toProcess = toProcess2;
            }

            return default;
        }

        public static List<T> GetAllDescendants<T>(
            this SyntaxNode s
            )
            where T : SyntaxNode
        {
            var r = s
                .DescendantNodes()
                .OfType<T>()
                .ToList()
                ;

            return r;
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
