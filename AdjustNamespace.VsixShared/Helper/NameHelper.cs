using Microsoft.CodeAnalysis;
using System;
using System.Reflection;

namespace AdjustNamespace
{
    /// <summary>
    /// Helpers which convert the Roslyn symbols and the reflection types into their string names.
    /// </summary>
    public static class NameHelper
    {
        /// <summary>
        /// Format of a full type name: namespace + containing types + type name.
        /// </summary>
        private static readonly SymbolDisplayFormat _symbolDisplayFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        //Roslyn is unable to produce a reflection-like name (`A.B.Class1`2`) out of the box:
        //the required option lives in the internal SymbolDisplayCompilerInternalOptions enum
        //and the constructor which accepts it is internal too, hence the reflection below
        private static readonly Type _symbolDisplayCompilerInternalOptionsType = typeof(SymbolDisplayFormat).Assembly.GetType("Microsoft.CodeAnalysis.SymbolDisplayCompilerInternalOptions")!;
        private static readonly ConstructorInfo _constructor = typeof(SymbolDisplayFormat).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            CallingConventions.Any,
            new Type[]
            {
                _symbolDisplayCompilerInternalOptionsType,
                typeof(SymbolDisplayGlobalNamespaceStyle),
                typeof(SymbolDisplayTypeQualificationStyle),
                typeof(SymbolDisplayGenericsOptions),
                typeof(SymbolDisplayMemberOptions),
                typeof(SymbolDisplayParameterOptions),

                typeof(SymbolDisplayDelegateStyle),
                typeof(SymbolDisplayExtensionMethodStyle),
                typeof(SymbolDisplayPropertyStyle),
                typeof(SymbolDisplayLocalOptions),
                typeof(SymbolDisplayKindOptions),
                typeof(SymbolDisplayMiscellaneousOptions)
            },
            null
            )!;
        const int _symbolDisplayCompilerInternalOptions_UseArityForGenericTypes = 2;

        private static readonly SymbolDisplayFormat _reflectionFormat = (SymbolDisplayFormat)_constructor.Invoke(
            new object[]
            {
                        _symbolDisplayCompilerInternalOptions_UseArityForGenericTypes,
                        SymbolDisplayGlobalNamespaceStyle.Omitted, //default(SymbolDisplayGlobalNamespaceStyle),
                        SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, //default(SymbolDisplayTypeQualificationStyle),
                        SymbolDisplayGenericsOptions.IncludeTypeParameters, //default(SymbolDisplayGenericsOptions),
                        default(SymbolDisplayMemberOptions),
                        default(SymbolDisplayDelegateStyle),

                        default(SymbolDisplayExtensionMethodStyle),
                        default(SymbolDisplayParameterOptions),
                        default(SymbolDisplayPropertyStyle),
                        default(SymbolDisplayLocalOptions),
                        default(SymbolDisplayKindOptions),
                        default(SymbolDisplayMiscellaneousOptions)
            });


        /// <summary>
        /// Full name of the reflection type prefixed with `global::` (if it has a namespace).
        /// </summary>
        /// <exception cref="Exception">The type has no full name (a generic parameter, for example).</exception>
        public static string ToGlobalDisplayString(
            this System.Type type
            )
        {
            var ds = type.FullName;

            if (ds == null)
            {
                throw new Exception("No full name exists");
            }

            if (!ds.Contains("."))
            {
                return ds;
            }

            return "global::" + ds;
        }

        /// <summary>
        /// Fully qualified (`global::`-prefixed by default) name of the symbol.
        /// </summary>
        public static string ToGlobalDisplayString(
            this ISymbol symbol,
            SymbolDisplayFormat? format = null
            )
        {
            var ds = symbol.ToDisplayString(format ?? SymbolDisplayFormat.FullyQualifiedFormat);

            return ds;
        }

        /// <summary>
        /// Name of the symbol in the given format (in the Roslyn default one if no format is given).
        /// </summary>
        public static string ToFullDisplayString(
            this ISymbol symbol,
            SymbolDisplayFormat? format = null
            )
        {
            var ds = symbol.ToDisplayString(format);

            return ds;
        }

        /// <summary>
        /// Full name of the symbol: namespace + containing types + name (without `global::`).
        /// </summary>
        public static string ToFullyQualifiedName(
            this ISymbol s
            )
        {
            if (s is null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            return s.ToDisplayString(_symbolDisplayFormat);
        }

        /// <summary>
        /// Name of the type in the reflection format, i.e. the format
        /// <c>System.Type.GetType</c> understands.
        /// </summary>
        /// <param name="s">The type.</param>
        /// <param name="deepLevel">Append the assembly name (required for the generic arguments).</param>
        /// <exception cref="Exception">A generic argument is not a named type.</exception>
        public static string ToReflectionFormat(
            this INamedTypeSymbol s,
            bool deepLevel = false
            )
        {
            if (s is null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            var reflectionTypeString = s.ToDisplayString(_reflectionFormat);
            if (s.TypeArguments.Length > 0)
            {
                reflectionTypeString += "[";
                foreach (var gta in s.TypeArguments)
                {
                    if (gta is not INamedTypeSymbol ngta)
                    {
                        throw new Exception(
                            $"[{gta.ToDisplayString()}] is not a {nameof(INamedTypeSymbol)}"
                            );
                    }

                    reflectionTypeString += $"[{ngta.ToReflectionFormat(true)}]";
                    reflectionTypeString += ",";
                }
                reflectionTypeString = reflectionTypeString.Substring(0, reflectionTypeString.Length - 1) + "]";
            }

            if (deepLevel)
            {
                reflectionTypeString += ", " + s.ContainingAssembly.ToDisplayString();
            }

            return reflectionTypeString;
        }
    }
}