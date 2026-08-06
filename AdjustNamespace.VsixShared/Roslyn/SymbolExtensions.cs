using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AdjustNamespace.Roslyn
{
    /// <summary>
    /// Questions asked of the Roslyn symbols: what a namespace contains and whether it
    /// keeps existing without the file which is being moved out of it.
    /// The walk over the syntax trees is in <see cref="SyntaxExtensions"/>.
    /// </summary>
    public static class SymbolExtensions
    {
        /// <summary>
        /// All the types (including the nested ones) of the namespace and of its child namespaces.
        /// </summary>
        public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol @namespace)
        {
            foreach (var type in @namespace.GetTypeMembers())
                foreach (var nestedType in type.GetNestedTypes())
                    yield return nestedType;

            foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
                foreach (var type in nestedNamespace.GetAllTypes())
                    yield return type;
        }


        /// <summary>
        /// The namespace with the given full name, as this compilation sees it
        /// (its own code plus everything it references).
        /// </summary>
        /// <returns><c>null</c> if there is no such namespace in this compilation.</returns>
        public static INamespaceSymbol? TryFindNamespace(
            this Compilation compilation,
            string namespaceName
            )
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            if (namespaceName is null)
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }

            INamespaceSymbol? result = compilation.GlobalNamespace;

            foreach (var part in namespaceName.Split('.'))
            {
                result = result!
                    .GetNamespaceMembers()
                    .FirstOrDefault(n => n.Name == part)
                    ;

                if (result == null)
                {
                    return null;
                }
            }

            return result;
        }

        /// <summary>
        /// The namespace contains at least one type which is not declared in the given file.
        ///
        /// This is the question `does this namespace still exist for this project after the
        /// given file has been moved out of it`: a namespace is emptied for the whole solution,
        /// but a `using` clause is resolved against a single project, and another project may
        /// fill that namespace without this one referencing it at all.
        /// </summary>
        /// <param name="compilation">Compilation of the project the question is asked for.</param>
        /// <param name="namespaceName">Full name of the namespace.</param>
        /// <param name="filePath">Full path of the file which is being moved out of it.</param>
        public static bool IsNamespaceFilledOutside(
            this Compilation compilation,
            string namespaceName,
            string filePath
            )
        {
            var @namespace = compilation.TryFindNamespace(namespaceName);
            if (@namespace == null)
            {
                return false;
            }

            foreach (var type in @namespace.GetAllTypes())
            {
                if (type.DeclaringSyntaxReferences.Length == 0)
                {
                    //a type of a referenced assembly, it stays where it is
                    return true;
                }

                //a partial type of the file which is being moved: the generated part of it
                //(the code behind of a xaml file) is regenerated out of the sources we are
                //adjusting right now and follows the type into the target namespace,
                //so it does not keep this namespace alive
                var isDeclaredInTheFile = false;
                foreach (var reference in type.DeclaringSyntaxReferences)
                {
                    if (string.Equals(reference.SyntaxTree.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        isDeclaredInTheFile = true;
                        break;
                    }
                }

                foreach (var reference in type.DeclaringSyntaxReferences)
                {
                    if (string.Equals(reference.SyntaxTree.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (isDeclaredInTheFile && GeneratedCode.IsGeneratedFile(reference.SyntaxTree.FilePath))
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The type itself and all the types nested into it (recursively).
        /// </summary>
        public static IEnumerable<INamedTypeSymbol> GetNestedTypes(this INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nestedType in type.GetTypeMembers()
                .SelectMany(nestedType => nestedType.GetNestedTypes()))
                yield return nestedType;
        }

    }
}
