using AdjustNamespace.Helper;
using AdjustNamespace.Xaml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdjustNamespace.Adjusting.Fixer;
using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Namespace;
using AdjustNamespace.Adjusting.Fixer.Specific;
using AdjustNamespace.Adjusting.Adjuster.Cs;

namespace AdjustNamespace.Adjusting
{
    /// <summary>
    /// Adjuster for cs file.
    /// </summary>
    public class CsAdjuster : IAdjuster
    {
        private readonly VsServices _vss;
        private readonly bool _openFilesToEnableUndo;
        private readonly NamespaceCenter _namespaceCenter;
        private readonly string _subjectFilePath;
        private readonly string _targetNamespace;
        private readonly List<string> _xamlFilePaths;

        public CsAdjuster(
            VsServices vss,
            bool openFilesToEnableUndo,
            NamespaceCenter namespaceCenter,
            string subjectFilePath,
            string targetNamespace,
            List<string> xamlFilePaths
            )
        {
            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            if (subjectFilePath is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePath));
            }

            if (targetNamespace is null)
            {
                throw new ArgumentNullException(nameof(targetNamespace));
            }

            if (xamlFilePaths is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePaths));
            }

            _vss = vss;
            _openFilesToEnableUndo = openFilesToEnableUndo;
            _namespaceCenter = namespaceCenter;
            _subjectFilePath = subjectFilePath;
            _targetNamespace = targetNamespace;
            _xamlFilePaths = xamlFilePaths;
        }

        public async Task<bool> AdjustAsync()
        {
            var (subjectDocument, subjectSyntaxRoot) = await _vss.Workspace.GetDocumentAndSyntaxRootAsync(_subjectFilePath);
            if (subjectDocument == null || subjectSyntaxRoot == null)
            {
                //skip this document
                return false;
            }

            var subjectSemanticModel = await subjectDocument.GetSemanticModelAsync();
            if (subjectSemanticModel == null)
            {
                //skip this document
                return false;
            }

            var ntc = NamespaceTransitionContainer.GetNamespaceTransitionsFor(subjectSyntaxRoot, _targetNamespace);
            if (ntc.IsEmpty)
            {
                //skip this document
                return false;
            }

            var fixerContainer = new FixerContainer(_vss, _openFilesToEnableUndo);

            var processedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            //fix refs (adding a new using namespace clauses or edit fully qualified names)
            await FixReferencesAsync(
                processedTypes,
                subjectSyntaxRoot,
                subjectSemanticModel,
                ntc,
                fixerContainer
                );

            //fix namespaces of the current file
            fixerContainer.Fixer<NamespaceFixer>(subjectDocument.FilePath!)
                .AddSubject(ntc)
                ;

            //perform fixing
            await fixerContainer.FixAllAsync();

            //TODO: switch to IFixer infrastructure, and put above fixerContainer.FixAllAsync() clause
            await FixReferenceInXamlFilesAsync(
                ntc,
                processedTypes
                );

            return true;
        }

        private async Task FixReferencesAsync(
            HashSet<INamedTypeSymbol> processedTypes,
            SyntaxNode syntaxRoot,
            SemanticModel semanticModel,
            NamespaceTransitionContainer ntc,
            FixerContainer fixerContainer
            )
        {

            var foundSyntaxes = (
                from snode in syntaxRoot.DescendantNodes()
                where snode is TypeDeclarationSyntax || snode is EnumDeclarationSyntax || snode is DelegateDeclarationSyntax
                select snode
                ).ToList();

            foreach (var foundTypeSyntax in foundSyntaxes)
            {
                var symbolInfo = (INamedTypeSymbol?)semanticModel.GetDeclaredSymbol(foundTypeSyntax);
                if (symbolInfo == null)
                {
                    //skip this type
                    continue;
                }

                if (processedTypes.Contains(symbolInfo))
                {
                    //already processed
                    continue;
                }

                var symbolNamespace = symbolInfo.ContainingNamespace.ToDisplayString();
                if (symbolNamespace == _targetNamespace)
                {
                    continue;
                }

                if (NamespaceHelper.IsSpecialNamespace(symbolNamespace))
                {
                    continue;
                }

                var targetNamespaceInfo = ntc.TransitionDict[symbolNamespace];

                if (symbolNamespace == targetNamespaceInfo.ModifiedName)
                {
                    //current symbol is in target namespace already
                    continue;
                }

                //create fixers for all references
                var refProcessor = new RefProcessor(_vss, fixerContainer, targetNamespaceInfo);
                await refProcessor.ProcessRefsAsync(symbolInfo);

                processedTypes.Add(symbolInfo);
                _namespaceCenter.TypeRemoved(symbolInfo);
            }
        }

        private async System.Threading.Tasks.Task FixReferenceInXamlFilesAsync(
            NamespaceTransitionContainer ntc,
            HashSet<INamedTypeSymbol> processedTypes
            )
        {
            if (processedTypes is null)
            {
                throw new ArgumentNullException(nameof(processedTypes));
            }

            foreach (var xamlFilePath in _xamlFilePaths)
            {
                if (!xamlFilePath.EndsWith(".xaml"))
                {
                    continue;
                }

                var xamlEngine = new XamlEngine(_vss);

                var testDocument = await xamlEngine.CreateDocumentAsync(false, xamlFilePath);

                var modifiedTestDocument = PerformChanges(
                    testDocument,
                    ntc,
                    processedTypes
                    );

                //open XAML files only if changes exists
                if (modifiedTestDocument.IsChangesExists(testDocument))
                {
                    var realDocument = await xamlEngine.CreateDocumentAsync(_openFilesToEnableUndo, xamlFilePath);

                    var modifiedRealDocument = PerformChanges(
                        realDocument,
                        ntc,
                        processedTypes
                        );

                    modifiedRealDocument.SaveIfChangesExistsAgainst(realDocument);
                }
            }
        }

        private XamlDocument PerformChanges(
            XamlDocument document,
            NamespaceTransitionContainer ntc,
            HashSet<INamedTypeSymbol> processedTypes
            )
        {
            var result = document;

            foreach (var processedType in processedTypes)
            {
                var targetNamespaceInfo = ntc.TransitionDict[processedType.ContainingNamespace.ToDisplayString()];

                result = result.MoveObject(
                    processedType.ContainingNamespace.ToDisplayString(),
                    processedType.Name,
                    targetNamespaceInfo.ModifiedName
                    );
            }

            return result;
        }
    }
}
