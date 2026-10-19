using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AdjustNamespace.TextReferences
{
    /// <summary>
    /// Finds the parts of a C# file that may name a type, with Roslyn syntax: the string
    /// literals of every kind (regular, verbatim, raw, UTF-8, the text parts of an interpolated
    /// string), and, if asked for, the comments and the text of the documentation comments.
    /// The spans are exact, because they are the spans of the tokens.
    ///
    /// A string that is passed as a type name is told from the others, as it is worth more:
    /// <c>Type.GetType</c>, <c>Assembly.GetType</c>, <c>Activator.CreateInstance</c>,
    /// <c>AppDomain.CreateInstance*</c>, <c>new ResourceManager(baseName, ...)</c>, the
    /// attributes <c>[TypeConverter]</c>, <c>[Designer]</c>, <c>[Editor]</c>,
    /// <c>[DebuggerTypeProxy]</c>, <c>[TypeForwardedFrom]</c> and any parameter named
    /// <c>typeName</c>, <c>assemblyQualifiedName</c>, <c>className</c> or <c>baseName</c>.
    /// The semantic model tells them when there is one; without it the names of the methods are
    /// all there is.
    /// </summary>
    internal static class CSharpTextScanner
    {
        private static readonly HashSet<string> TypeNameParameters = new(StringComparer.Ordinal)
        {
            "typeName",
            "assemblyQualifiedName",
            "className",
            "baseName"
        };

        private static readonly HashSet<string> TypeNameAttributes = new(StringComparer.Ordinal)
        {
            "TypeConverter",
            "Designer",
            "Editor",
            "DebuggerTypeProxy",
            "TypeForwardedFrom"
        };

        /// <summary>
        /// Methods whose string argument is a type name even if the parameter is not called so.
        /// </summary>
        private static readonly HashSet<string> TypeNameMethods = new(StringComparer.Ordinal)
        {
            "System.Type.GetType",
            "System.Reflection.Assembly.GetType",
            "System.Reflection.Assembly.CreateInstance",
            "System.Activator.CreateInstance",
            "System.Activator.CreateInstanceFrom",
            "System.AppDomain.CreateInstance",
            "System.AppDomain.CreateInstanceFrom",
            "System.AppDomain.CreateInstanceAndUnwrap",
            "System.AppDomain.CreateInstanceFromAndUnwrap"
        };

        /// <summary>
        /// The names of the methods that take a type name, for a call the semantic model
        /// knows nothing about.
        /// </summary>
        private static readonly HashSet<string> TypeNameMethodNames = new(StringComparer.Ordinal)
        {
            "GetType",
            "CreateInstance",
            "CreateInstanceFrom",
            "CreateInstanceAndUnwrap",
            "CreateInstanceFromAndUnwrap"
        };

        /// <summary>
        /// The candidates of a syntax tree.
        /// </summary>
        /// <param name="root">The syntax tree of the file.</param>
        /// <param name="semanticModel">The semantic model of the file, asked for only when a string literal needs it; may return <c>null</c>.</param>
        /// <param name="includeComments">Look into the comments as well.</param>
        /// <param name="cancellationToken">Cancellation of the session.</param>
        public static List<TextCandidate> Scan(
            SyntaxNode root,
            Func<SemanticModel?> semanticModel,
            bool includeComments,
            CancellationToken cancellationToken = default
            )
        {
            var result = new List<TextCandidate>();

            foreach (var token in root.DescendantTokens())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryGetContent(token, out var start, out var length))
                {
                    continue;
                }

                result.Add(
                    new TextCandidate(
                        PlaceOf(token, semanticModel),
                        start,
                        length
                        )
                    );
            }

            if (includeComments)
            {
                AddComments(root, result);
            }

            return result;
        }

        /// <summary>
        /// The span of the text inside of the quotes of a string token.
        /// </summary>
        private static bool TryGetContent(
            SyntaxToken token,
            out int start,
            out int length
            )
        {
            start = 0;
            length = 0;

            var text = token.Text;

            switch (token.Kind())
            {
                case SyntaxKind.InterpolatedStringTextToken:
                    start = token.SpanStart;
                    length = token.Span.Length;
                    return length > 0;

                case SyntaxKind.StringLiteralToken:
                case SyntaxKind.Utf8StringLiteralToken:
                    {
                        var end = text.Length;
                        if (token.IsKind(SyntaxKind.Utf8StringLiteralToken) && end >= 2)
                        {
                            end -= 2;
                        }

                        var open = text.StartsWith("@\"", StringComparison.Ordinal) ? 2 : 1;

                        if (end - open - 1 < 0 || text[open - 1] != '"')
                        {
                            return false;
                        }

                        start = token.SpanStart + open;
                        length = end - open - 1;
                        return length > 0;
                    }

                case SyntaxKind.SingleLineRawStringLiteralToken:
                case SyntaxKind.MultiLineRawStringLiteralToken:
                case SyntaxKind.Utf8SingleLineRawStringLiteralToken:
                case SyntaxKind.Utf8MultiLineRawStringLiteralToken:
                    {
                        var end = text.Length;
                        if ((token.IsKind(SyntaxKind.Utf8SingleLineRawStringLiteralToken)
                            || token.IsKind(SyntaxKind.Utf8MultiLineRawStringLiteralToken)) && end >= 2)
                        {
                            end -= 2;
                        }

                        var quotes = 0;
                        while (quotes < end && text[quotes] == '"')
                        {
                            quotes++;
                        }

                        if (quotes < 3 || end - 2 * quotes < 0)
                        {
                            return false;
                        }

                        start = token.SpanStart + quotes;
                        length = end - 2 * quotes;
                        return length > 0;
                    }

                default:
                    return false;
            }
        }

        private static TextReferencePlace PlaceOf(
            SyntaxToken token,
            Func<SemanticModel?> semanticModel
            )
        {
            if (token.Parent is LiteralExpressionSyntax literal)
            {
                if (literal.Parent is ArgumentSyntax argument && IsTypeNameArgument(argument, semanticModel))
                {
                    return TextReferencePlace.KnownTypeNameArgument;
                }

                if (literal.Parent is AttributeArgumentSyntax attributeArgument
                    && IsTypeNameAttributeArgument(attributeArgument, semanticModel))
                {
                    return TextReferencePlace.KnownTypeNameArgument;
                }
            }

            return TextReferencePlace.CSharpStringLiteral;
        }

        private static bool IsTypeNameArgument(
            ArgumentSyntax argument,
            Func<SemanticModel?> semanticModel
            )
        {
            if (argument.Parent is not BaseArgumentListSyntax list)
            {
                return false;
            }

            var call = list.Parent;
            if (call is null)
            {
                return false;
            }

            var index = list.Arguments.IndexOf(argument);

            var method = TryGetMethod(call, semanticModel);
            if (method != null)
            {
                var parameter = ParameterOf(method, argument, index);

                if (parameter != null && TypeNameParameters.Contains(parameter.Name))
                {
                    return true;
                }

                return IsTypeNameMethod(method);
            }

            //nothing is known about the call but its name
            switch (call)
            {
                case InvocationExpressionSyntax invocation:
                    return TypeNameMethodNames.Contains(NameOf(invocation.Expression));

                case BaseObjectCreationExpressionSyntax creation:
                    return creation is ObjectCreationExpressionSyntax typed
                        && NameOf(typed.Type) == "ResourceManager"
                        && index == 0;

                default:
                    return false;
            }
        }

        private static bool IsTypeNameAttributeArgument(
            AttributeArgumentSyntax argument,
            Func<SemanticModel?> semanticModel
            )
        {
            if (argument.Parent?.Parent is not AttributeSyntax attribute)
            {
                return false;
            }

            var name = NameOf(attribute.Name);
            if (name.EndsWith("Attribute", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - "Attribute".Length);
            }

            if (TypeNameAttributes.Contains(name))
            {
                return true;
            }

            if (argument.NameColon != null || argument.NameEquals != null)
            {
                return false;
            }

            var method = TryGetMethod(attribute, semanticModel);
            if (method == null || attribute.ArgumentList == null)
            {
                return false;
            }

            var index = attribute.ArgumentList.Arguments.IndexOf(argument);
            if (index < 0 || index >= method.Parameters.Length)
            {
                return false;
            }

            return TypeNameParameters.Contains(method.Parameters[index].Name);
        }

        private static IMethodSymbol? TryGetMethod(
            SyntaxNode call,
            Func<SemanticModel?> semanticModel
            )
        {
            var model = semanticModel();
            if (model == null)
            {
                return null;
            }

            var info = model.GetSymbolInfo(call);

            return info.Symbol as IMethodSymbol
                ?? info.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
        }

        private static IParameterSymbol? ParameterOf(
            IMethodSymbol method,
            ArgumentSyntax argument,
            int index
            )
        {
            if (argument.NameColon != null)
            {
                var name = argument.NameColon.Name.Identifier.ValueText;

                return method.Parameters.FirstOrDefault(p => p.Name == name);
            }

            if (index < 0 || method.Parameters.Length == 0)
            {
                return null;
            }

            return index < method.Parameters.Length
                ? method.Parameters[index]
                : method.Parameters[method.Parameters.Length - 1];
        }

        private static bool IsTypeNameMethod(IMethodSymbol method)
        {
            if (method.MethodKind == MethodKind.Constructor)
            {
                return method.ContainingType.ToDisplayString() == "System.Resources.ResourceManager";
            }

            return TypeNameMethods.Contains(method.ContainingType.ToDisplayString() + "." + method.Name);
        }

        /// <summary>
        /// The last identifier of a name or of a member access: <c>Type.GetType</c> -> <c>GetType</c>.
        /// </summary>
        private static string NameOf(SyntaxNode node)
        {
            switch (node)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    return memberAccess.Name.Identifier.ValueText;

                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText;

                case GenericNameSyntax generic:
                    return generic.Identifier.ValueText;

                case QualifiedNameSyntax qualified:
                    return NameOf(qualified.Right);

                case AliasQualifiedNameSyntax alias:
                    return NameOf(alias.Name);

                default:
                    return string.Empty;
            }
        }

        private static void AddComments(
            SyntaxNode root,
            List<TextCandidate> result
            )
        {
            foreach (var trivia in root.DescendantTrivia())
            {
                switch (trivia.Kind())
                {
                    case SyntaxKind.SingleLineCommentTrivia:
                    case SyntaxKind.MultiLineCommentTrivia:
                        result.Add(new TextCandidate(TextReferencePlace.Comment, trivia.SpanStart, trivia.Span.Length));
                        break;

                    case SyntaxKind.SingleLineDocumentationCommentTrivia:
                    case SyntaxKind.MultiLineDocumentationCommentTrivia:
                        AddDocumentationText(trivia, result);
                        break;
                }
            }
        }

        /// <summary>
        /// The text of a documentation comment. A <c>cref</c> is not a text: Roslyn handles
        /// it as a reference, and it is not among the text nodes.
        /// </summary>
        private static void AddDocumentationText(
            SyntaxTrivia trivia,
            List<TextCandidate> result
            )
        {
            if (trivia.GetStructure() is not DocumentationCommentTriviaSyntax structure)
            {
                return;
            }

            foreach (var text in structure.DescendantNodes().OfType<XmlTextSyntax>())
            {
                foreach (var token in text.TextTokens)
                {
                    if (!token.IsKind(SyntaxKind.XmlTextLiteralToken) || token.Span.Length == 0)
                    {
                        continue;
                    }

                    result.Add(new TextCandidate(TextReferencePlace.Comment, token.SpanStart, token.Span.Length));
                }
            }
        }
    }
}
