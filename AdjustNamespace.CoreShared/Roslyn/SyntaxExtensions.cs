using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Roslyn
{
    /// <summary>
    /// Extensions which walk and modify the Roslyn syntax trees.
    /// The questions asked of the symbols are in <see cref="SymbolExtensions"/>.
    /// </summary>
    public static class SyntaxExtensions
    {
        /// <summary>
        /// Climb up the qualified name (<c>A.B.C.Class1.NestedClass2</c>) until the left part
        /// of the name stops being a type, i.e. until the namespace part is reached.
        /// </summary>
        /// <returns>
        /// The outermost qualified name whose left part is a namespace,
        /// or <c>null</c> if there is no namespace in that name at all
        /// (<c>Class1.NestedClass2</c>, for example).
        /// </returns>
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

        /// <summary>
        /// Check if the node starts with the `global::` alias.
        /// </summary>
        public static bool IsGlobal(this SyntaxNode node)
        {
            if (node is null)
            {
                throw new System.ArgumentNullException(nameof(node));
            }

            return node.ToString().StartsWith("global::");
        }

        /// <summary>
        /// Climb up the syntax tree while the parent node is of the type <typeparamref name="T"/>,
        /// i.e. return the outermost node of that type in this chain.
        /// </summary>
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

        /// <summary>
        /// Breadth-first search for the closest descendant (or the node itself)
        /// of exactly the given type.
        /// </summary>
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

        /// <summary>
        /// Add a using clause in front of the existing ones.
        ///
        /// The header of a file (a license comment, for example) belongs to the leading trivia
        /// of the first token of that file. A new clause added into a file without any using
        /// becomes the first token and the header slides below it, hence the header is moved
        /// onto the new clause here.
        /// </summary>
        public static CompilationUnitSyntax AddUsingKeepingHeader(
            this CompilationUnitSyntax cus,
            UsingDirectiveSyntax newUsing
            )
        {
            if (cus.Usings.Count > 0)
            {
                //the header belongs to the first of them and stays where it is
                return cus.AddUsings(newUsing);
            }

            var header = cus.GetLeadingTrivia();

            return cus
                .WithLeadingTrivia()
                .AddUsings(
                    newUsing.WithLeadingTrivia(header)
                    );
        }

        /// <summary>
        /// The indentation of the given node, i.e. the whitespace of the line it starts at.
        ///
        /// The rest of its leading trivia (the comments and the directives like
        /// <c>#region</c> above it) belongs to that node only and must not be copied
        /// onto a node placed next to it.
        /// </summary>
        public static SyntaxTriviaList GetIndentationOf(this SyntaxNode node)
        {
            var leadingTrivia = node.GetLeadingTrivia();

            var result = new List<SyntaxTrivia>();
            for (var i = leadingTrivia.Count - 1; i >= 0; i--)
            {
                if (!leadingTrivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
                {
                    break;
                }

                result.Insert(0, leadingTrivia[i]);
            }

            return SyntaxFactory.TriviaList(result);
        }

        /// <summary>
        /// All the descendants of the given type.
        /// </summary>
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
    }
}
