using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Namespace
{
    /// <summary>
    /// Helpers around the namespaces: the namespaces we must never touch and the search for
    /// the namespace declarations in a syntax tree.
    ///
    /// The rule which determines the target namespace of a file used to live here as an
    /// extension method on the <c>SolutionItem</c> of the solution tree; it is
    /// <c>Namespace.TargetNamespaceResolver</c> now.
    /// </summary>
    public static class NamespaceHelper
    {
        /// <summary>
        /// Check if the namespace is a special one, i.e. the one we must never touch.
        /// </summary>
        public static bool IsSpecialNamespace(
            string namespaceName
            )
        {
            //workaround: we will not remove a System* and Microsoft* namespaces
            //such namespaces may exists in the codebase because of
            //delivering special attributes in the obsolete codebase
            //(like nullable attributes, CallerMemberNameAttribute etc)
            //we do not want to remove System, System.*, Microsoft.* from
            //the codebase in this case

            //only the first part of the name is compared: a namespace which merely
            //begins with these words (SystemX.Utils, MicrosoftPatterns.Prism)
            //belongs to the user and has to be adjusted as usual
            var dotIndex = namespaceName.IndexOf('.');
            var firstPart = dotIndex >= 0
                ? namespaceName.Substring(0, dotIndex)
                : namespaceName
                ;

            if (firstPart == "System")
            {
                return true;
            }
            if (firstPart == "Microsoft")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// The name of a using clause, as the namespaces are named here.
        /// The clause is taken from the document as it is written by the user, and it may
        /// contain the whitespace between the parts of the name (<c>using A . B;</c>)
        /// and the <c>global::</c> alias (<c>using global::A.B;</c>).
        /// </summary>
        public static string NormalizeUsingName(
            string name
            )
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var builder = new System.Text.StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (!char.IsWhiteSpace(c))
                {
                    builder.Append(c);
                }
            }

            var result = builder.ToString();

            const string GlobalPrefix = "global::";
            if (result.StartsWith(GlobalPrefix, StringComparison.Ordinal))
            {
                result = result.Substring(GlobalPrefix.Length);
            }

            return result;
        }

        /// <summary>
        /// Find all the declarations of the given namespace in the document.
        /// </summary>
        /// <param name="syntaxRoot">Syntax root of the document.</param>
        /// <param name="namespaceName">Full name of the namespace to search for.</param>
        /// <param name="result">(out) Found declarations.</param>
        /// <returns><c>true</c> if at least one declaration has been found.</returns>
        public static bool TryFindNamespaceNodesFor(
            this SyntaxNode syntaxRoot,
            string namespaceName,
            out List<BaseNamespaceDeclarationSyntax> result
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
                .OfType<BaseNamespaceDeclarationSyntax>()
                .ToList();


            foreach (var foundNamespaceSyntax in allFoundNamespaceSyntaxes)
            {
                //the full name and not the written fragment: a nested `namespace A` inside
                //`Wrapping` is `Wrapping.A`, and renaming a root `A` must not touch it
                if (FullNameOf(foundNamespaceSyntax) == namespaceName)
                {
                    result.Add(foundNamespaceSyntax);
                }
            }

            return result.Count > 0;
        }

        /// <summary>
        /// The full name of a namespace declaration, including the names of the enclosing
        /// ones for a nested classic declaration.
        /// </summary>
        private static string FullNameOf(
            BaseNamespaceDeclarationSyntax declaration
            )
        {
            var parts = new List<string>();

            SyntaxNode? node = declaration;
            while (node != null)
            {
                if (node is BaseNamespaceDeclarationSyntax ns)
                {
                    parts.Add(ns.Name.ToString());
                }

                node = node.Parent;
            }

            parts.Reverse();

            return string.Join(".", parts);
        }
    }
}
