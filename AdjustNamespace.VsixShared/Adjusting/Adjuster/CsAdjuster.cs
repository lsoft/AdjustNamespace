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
    ///
    /// The workflow is:
    /// 1) determine the namespace transitions (old namespace -> new namespace) for the file;
    /// 2) find every reference to the types declared in this file and schedule a fix for each
    ///    of them (a new using clause or an edited fully qualified name), see <see cref="RefProcessor"/>;
    /// 3) schedule the fix of the namespace clauses of the file itself;
    /// 4) apply all the scheduled fixes at once, see <see cref="FixerContainer"/>;
    /// 5) fix the references to the moved types in the xaml files of the solution.
    /// </summary>
    public class CsAdjuster : IAdjuster
    {
        private readonly VsServices _vss;
        private readonly bool _openFilesToEnableUndo;
        private readonly NamespaceCenter _namespaceCenter;
        private readonly string _subjectFilePath;
        private readonly string _targetNamespace;
        private readonly List<string> _xamlFilePaths;

        /// <param name="vss">Visual Studio services.</param>
        /// <param name="openFilesToEnableUndo">Open the changed files in the editor (this allows the user to undo the changes).</param>
        /// <param name="namespaceCenter">Namespace state container shared across the whole adjusting session.</param>
        /// <param name="subjectFilePath">Full path to the C# file to adjust.</param>
        /// <param name="targetNamespace">The namespace the types of that file should be moved into.</param>
        /// <param name="xamlFilePaths">All the xaml files of the solution (they may reference the moved types).</param>
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

        /// <inheritdoc/>
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

        /// <summary>
        /// Create the fixers for every reference to every type declared in the subject file.
        /// </summary>
        /// <param name="processedTypes">
        /// (in/out) The types which have been moved. It is filled here and reused later
        /// to fix the references in the xaml files.
        /// </param>
        /// <param name="syntaxRoot">Syntax root of the subject file.</param>
        /// <param name="semanticModel">Semantic model of the subject file.</param>
        /// <param name="ntc">Namespace transitions of the subject file.</param>
        /// <param name="fixerContainer">(out) Container the created fixers are placed into.</param>
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

        /// <summary>
        /// Fix the references to the moved types in the xaml files of the solution.
        /// To keep it fast, the changes are firstly applied to an in-memory copy of the file,
        /// and only if that copy differs from the original one the real (possibly opened
        /// in the editor) document is touched.
        /// </summary>
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

        /// <summary>
        /// Apply the namespace transitions of every moved type to the given xaml document.
        /// <see cref="XamlDocument"/> is immutable, hence a new document is returned.
        /// </summary>
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
