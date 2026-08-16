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
                var fnn = foundNamespaceSyntax.Name.ToString();
                if (fnn == namespaceName)
                {
                    result.Add(foundNamespaceSyntax);
                }
            }

            return result.Count > 0;
        }
    }
}
