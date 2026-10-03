using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Namespace;
using AdjustNamespace.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AdjustNamespace.Adjusting.Adjuster.Cs
{
    /// <summary>
    /// Fixer of the <c>[assembly: XmlnsDefinition("uri", "A.B")]</c> attributes of WPF, Avalonia
    /// and .NET MAUI (including the global xmlns of .NET MAUI 10). Such an attribute maps a xml
    /// namespace onto a CLR namespace of the same assembly, and the CLR namespace is a string
    /// for Roslyn: nothing else follows the move, and the xaml which uses the uri stops finding
    /// the moved classes at run time.
    ///
    /// When the old namespace is left empty, the string is replaced with the new namespace.
    /// When the old namespace keeps other types, the uri has to map both of them, so a copy of
    /// the attribute is added for the new namespace.
    /// </summary>
    public readonly struct XmlnsDefinitionFixer
    {
        private const string AttributeName = "XmlnsDefinition";

        private readonly Workspace _workspace;
        private readonly EditSet _edits;
        private readonly string _subjectFilePath;

        /// <param name="workspace">The workspace of the solution.</param>
        /// <param name="edits">Set the scheduled edits are placed into.</param>
        /// <param name="subjectFilePath">The file being adjusted.</param>
        public XmlnsDefinitionFixer(
            Workspace workspace,
            EditSet edits,
            string subjectFilePath
            )
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _edits = edits ?? throw new ArgumentNullException(nameof(edits));
            _subjectFilePath = subjectFilePath ?? throw new ArgumentNullException(nameof(subjectFilePath));
        }

        /// <summary>
        /// Schedule the edits of the attributes which map the namespaces the subject file
        /// moves its types out of.
        /// </summary>
        /// <param name="transitions">The transitions of the types which really move.</param>
        /// <param name="cancellationToken">Cancellation of the session.</param>
        public async Task FixAsync(
            IReadOnlyCollection<NamespaceTransition> transitions,
            CancellationToken cancellationToken = default
            )
        {
            if (transitions is null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            if (transitions.Count == 0)
            {
                return;
            }

            //the attribute maps the namespaces of its own assembly, i.e. of the projects
            //which compile the subject file
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var subjectDocument in _workspace.GetDocuments(_subjectFilePath))
            {
                var project = subjectDocument.Project;

                var compilation = await project.GetCompilationAsync(cancellationToken);
                if (compilation == null)
                {
                    continue;
                }

                var definitions = await ReadDefinitionsAsync(project, cancellationToken);
                if (definitions.Count == 0)
                {
                    continue;
                }

                foreach (var transition in transitions.Distinct())
                {
                    var isOriginalAlive = transition.KeepsOriginalAlive
                        || compilation.IsNamespaceFilledOutside(transition.OriginalName, _subjectFilePath);

                    foreach (var definition in definitions.Where(d => d.ClrNamespace == transition.OriginalName))
                    {
                        if (!processedFiles.Add(definition.FilePath + "|" + definition.Literal.SpanStart + "|" + transition.ModifiedName))
                        {
                            //the same file of another target framework
                            continue;
                        }

                        Fix(definition, definitions, transition, isOriginalAlive);
                    }
                }
            }
        }

        private void Fix(
            Definition definition,
            List<Definition> definitions,
            NamespaceTransition transition,
            bool isOriginalAlive
            )
        {
            var targetExists = definitions.Any(
                d => d.XmlNamespace == definition.XmlNamespace && d.ClrNamespace == transition.ModifiedName
                );

            if (isOriginalAlive)
            {
                if (targetExists)
                {
                    return;
                }

                //the uri maps both namespaces from now on: a copy of the attribute list
                //(without its trivia) is written on the next line
                var list = definition.AttributeList;
                var listText = list.ToString();
                var literalStart = definition.Literal.SpanStart - list.SpanStart;
                var copy = listText.Substring(0, literalStart)
                    + Quote(transition.ModifiedName)
                    + listText.Substring(literalStart + definition.Literal.Span.Length);

                AdjustLog.WriteLine($"[Adjust] XmlnsDefinitionFixer: {definition.FilePath}: {transition.OriginalName} stays alive, add a mapping of {transition.ModifiedName}");

                _edits.ReplaceText(
                    definition.FilePath,
                    new TextSpan(list.Span.End, 0),
                    LineBreakOf(list) + copy
                    );
                return;
            }

            if (targetExists && definition.AttributeList.Attributes.Count == 1)
            {
                //the uri maps the new namespace already, and the old one is left empty:
                //the attribute goes away with its line, a comment above it stays
                AdjustLog.WriteLine($"[Adjust] XmlnsDefinitionFixer: {definition.FilePath}: remove the mapping of the emptied {transition.OriginalName}");

                var list = definition.AttributeList;

                _edits.ReplaceText(
                    definition.FilePath,
                    TextSpan.FromBounds(list.SpanStart, list.FullSpan.End),
                    string.Empty
                    );
                return;
            }

            AdjustLog.WriteLine($"[Adjust] XmlnsDefinitionFixer: {definition.FilePath}: {transition.OriginalName} -> {transition.ModifiedName}");

            _edits.ReplaceText(
                definition.FilePath,
                definition.Literal.Span,
                Quote(transition.ModifiedName)
                );
        }

        /// <summary>
        /// The line break the file is written with, taken from around the given node.
        /// </summary>
        private static string LineBreakOf(SyntaxNode node)
        {
            var text = node.SyntaxTree.GetText().ToString();

            if (text.Contains("\r\n"))
            {
                return "\r\n";
            }

            return text.Contains("\n") ? "\n" : Environment.NewLine;
        }

        private static string Quote(string value)
        {
            return SyntaxFactory.Literal(value).ToString();
        }

        /// <summary>
        /// Every <c>[assembly: XmlnsDefinition(...)]</c> of the project whose arguments are
        /// two string literals.
        /// </summary>
        private static async Task<List<Definition>> ReadDefinitionsAsync(
            Microsoft.CodeAnalysis.Project project,
            CancellationToken cancellationToken
            )
        {
            var result = new List<Definition>();

            foreach (var document in project.Documents)
            {
                if (document.FilePath == null || GeneratedCode.IsGeneratedFile(document.FilePath))
                {
                    continue;
                }

                if (!(await document.GetSyntaxRootAsync(cancellationToken) is CompilationUnitSyntax root))
                {
                    continue;
                }

                foreach (var list in root.AttributeLists)
                {
                    if (list.Target == null || !list.Target.Identifier.IsKind(SyntaxKind.AssemblyKeyword))
                    {
                        continue;
                    }

                    foreach (var attribute in list.Attributes)
                    {
                        if (!IsXmlnsDefinition(attribute.Name) || attribute.ArgumentList == null)
                        {
                            continue;
                        }

                        var arguments = attribute.ArgumentList.Arguments;
                        if (arguments.Count < 2
                            || !(arguments[0].Expression is LiteralExpressionSyntax xmlNamespace)
                            || !(arguments[1].Expression is LiteralExpressionSyntax clrNamespace)
                            || !xmlNamespace.IsKind(SyntaxKind.StringLiteralExpression)
                            || !clrNamespace.IsKind(SyntaxKind.StringLiteralExpression)
                            )
                        {
                            continue;
                        }

                        result.Add(
                            new Definition(
                                document.FilePath,
                                list,
                                xmlNamespace.Token.ValueText,
                                clrNamespace
                                )
                            );
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// The attribute is <c>XmlnsDefinition</c> or <c>XmlnsDefinitionAttribute</c>,
        /// written with or without a namespace.
        /// </summary>
        private static bool IsXmlnsDefinition(NameSyntax name)
        {
            var simpleName = name switch
            {
                QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
                SimpleNameSyntax simple => simple.Identifier.ValueText,
                _ => string.Empty
            };

            return simpleName == AttributeName || simpleName == AttributeName + "Attribute";
        }

        /// <summary>
        /// A single <c>XmlnsDefinition</c> attribute.
        /// </summary>
        private readonly struct Definition
        {
            public readonly string FilePath;

            public readonly AttributeListSyntax AttributeList;

            public readonly string XmlNamespace;

            public readonly LiteralExpressionSyntax Literal;

            public string ClrNamespace => Literal.Token.ValueText;

            public Definition(
                string filePath,
                AttributeListSyntax attributeList,
                string xmlNamespace,
                LiteralExpressionSyntax literal
                )
            {
                FilePath = filePath;
                AttributeList = attributeList;
                XmlNamespace = xmlNamespace;
                Literal = literal;
            }
        }
    }
}
