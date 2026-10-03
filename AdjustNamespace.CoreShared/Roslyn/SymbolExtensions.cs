using AdjustNamespace;
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
        /// The types declared directly in this namespace (including nested types of those
        /// declarations). Types of the child namespaces are not included: a <c>using</c>
        /// clause of this namespace does not import them.
        /// </summary>
        public static IEnumerable<INamedTypeSymbol> GetDirectTypes(this INamespaceSymbol @namespace)
        {
            foreach (var type in @namespace.GetTypeMembers())
                foreach (var nestedType in type.GetNestedTypes())
                    yield return nestedType;
        }

        /// <summary>
        /// All the types (including the nested ones) of the namespace and of its child namespaces.
        /// </summary>
        public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol @namespace)
        {
            foreach (var type in @namespace.GetDirectTypes())
                yield return type;

            foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
                foreach (var type in nestedNamespace.GetAllTypes())
                    yield return type;
        }

        /// <summary>
        /// The type is an extension block of C# 14 (<c>extension(Cat cat) { ... }</c> inside
        /// a static class). Roslyn shows such a block as a nested type of its static class.
        /// </summary>
        public static bool IsExtensionBlock(this INamedTypeSymbol type)
        {
            return type.TypeKind == TypeKind.Extension;
        }

        /// <summary>
        /// The static class whose import makes the given member callable without writing
        /// that class: the class of a classic extension method (<c>this Cat cat</c>) or of
        /// an extension block member (an extension property, a static extension member, an
        /// extension operator or indexer).
        ///
        /// Such a call (<c>cat.Shout()</c>, <c>Cat.Create()</c>, <c>a + b</c>, <c>cat[0]</c>)
        /// writes neither the class nor its namespace and resolves only because a using
        /// clause or an enclosing namespace imports that class.
        /// </summary>
        /// <returns><c>null</c> if the member is visible without any import.</returns>
        public static INamedTypeSymbol? TryGetImportedExtensionContainer(this ISymbol member)
        {
            var containingType = member.ContainingType;
            if (containingType == null)
            {
                return null;
            }

            if (containingType.IsExtensionBlock())
            {
                return containingType.ContainingType;
            }

            if (member is IMethodSymbol method)
            {
                var extensionMethod = method.ReducedFrom ?? method;
                if (extensionMethod.IsExtensionMethod)
                {
                    return extensionMethod.ContainingType;
                }
            }

            return null;
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
        ///
        /// Only the types declared directly in the namespace are considered. Types of the
        /// child namespaces do not count: a <c>using</c> of the parent does not import them,
        /// and counting them would add a clause which later fails to compile once those child
        /// namespaces have moved away too (see FreeAIr / WpfHelpers).
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
                AdjustLog.WriteLine(
                    $"[Adjust] IsNamespaceFilledOutside: {compilation.AssemblyName}: {namespaceName} not found"
                    );
                return false;
            }

            foreach (var type in @namespace.GetDirectTypes())
            {
                if (type.DeclaringSyntaxReferences.Length == 0)
                {
                    //a type of a referenced assembly, it stays where it is
                    AdjustLog.WriteLine(
                        $"[Adjust] IsNamespaceFilledOutside: {compilation.AssemblyName}: {namespaceName} "
                        + $"kept by metadata type {type.ToDisplayString()}"
                        );
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

                    //a type of its own the build generates out of the xaml of the moved code
                    //behind (`Program` of WinUI in `App.g.i.cs`): it is written into the
                    //namespace of the `x:Class`, which follows the moved class
                    if (GeneratedCode.IsGeneratedOutOfXamlOf(reference.SyntaxTree.FilePath, filePath))
                    {
                        continue;
                    }

                    AdjustLog.WriteLine(
                        $"[Adjust] IsNamespaceFilledOutside: {compilation.AssemblyName}: {namespaceName} "
                        + $"kept by {type.ToDisplayString()} in {reference.SyntaxTree.FilePath} "
                        + $"(containing namespace {type.ContainingNamespace.ToDisplayString()})"
                        );
                    return true;
                }
            }

            AdjustLog.WriteLine(
                $"[Adjust] IsNamespaceFilledOutside: {compilation.AssemblyName}: {namespaceName} "
                + "has nothing outside the moved file"
                );
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
