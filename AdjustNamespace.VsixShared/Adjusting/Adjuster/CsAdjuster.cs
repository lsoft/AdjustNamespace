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
            if (_vss.Workspace.IsCompiledBySeveralProjects(_subjectFilePath))
            {
                //a file of a shared project which is referenced by several projects:
                //there is no target namespace which suits all of them
                //skip this document
                return false;
            }

            //a file may be compiled by several projects (the target frameworks of a multi target
            //project), and every one of them parses it with its own conditional compilation
            //symbols: a type which is declared under such a symbol exists in a part of these
            //trees only, so all of them are taken into account
            var subjectTrees = await GetSubjectTreesAsync();
            if (subjectTrees.Count == 0)
            {
                //skip this document
                return false;
            }

            var ntc = NamespaceTransitionContainer.GetNamespaceTransitionsFor(
                subjectTrees.ConvertAll(t => t.SyntaxRoot),
                _targetNamespace
                );
            if (ntc.IsEmpty)
            {
                //skip this document
                return false;
            }

            foreach (var transition in ntc.Transitions)
            {
                if (await _vss.Workspace.IsNamespaceStateContradictoryAsync(_subjectFilePath, transition.OriginalName))
                {
                    //the projects which compile this file do not agree whether the namespace
                    //it is moved out of stays alive, and there is a single text for all of them:
                    //whatever we do with the using clauses, one of these projects breaks
                    //skip this document
                    return false;
                }
            }

            var fixerContainer = new FixerContainer(_vss, _openFilesToEnableUndo);

            var processedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            //fix refs (adding a new using namespace clauses or edit fully qualified names)
            await FixReferencesAsync(
                processedTypes,
                subjectTrees,
                ntc,
                fixerContainer
                );

            //fix namespaces of the current file
            fixerContainer.Fixer<NamespaceFixer>(_subjectFilePath)
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
        /// The documents the subject file is compiled as, with their syntax trees and semantic
        /// models: one per project which compiles it, see <see cref="WorkspaceHelper.GetDocuments"/>.
        /// </summary>
        private async Task<List<SubjectTree>> GetSubjectTreesAsync()
        {
            var result = new List<SubjectTree>();

            foreach (var document in _vss.Workspace.GetDocuments(_subjectFilePath))
            {
                var syntaxRoot = await document.GetSyntaxRootAsync();
                if (syntaxRoot == null)
                {
                    //skip this document
                    continue;
                }

                var semanticModel = await document.GetSemanticModelAsync();
                if (semanticModel == null)
                {
                    //skip this document
                    continue;
                }

                result.Add(new SubjectTree(syntaxRoot, semanticModel));
            }

            return result;
        }

        /// <summary>
        /// Create the fixers for every reference to every type declared in the subject file.
        /// </summary>
        /// <param name="processedTypes">
        /// (in/out) The types which have been moved. It is filled here and reused later
        /// to fix the references in the xaml files.
        /// </param>
        /// <param name="subjectTrees">The syntax trees of the subject file, one per project which compiles it.</param>
        /// <param name="ntc">Namespace transitions of the subject file.</param>
        /// <param name="fixerContainer">(out) Container the created fixers are placed into.</param>
        private async Task FixReferencesAsync(
            HashSet<INamedTypeSymbol> processedTypes,
            List<SubjectTree> subjectTrees,
            NamespaceTransitionContainer ntc,
            FixerContainer fixerContainer
            )
        {
            //the same type is a separate symbol in every project which compiles the file,
            //and the reference search of Roslyn covers all of them at once, so it is enough
            //to process the first symbol of every type
            var processedTypeNames = new HashSet<string>();

            foreach (var subjectTree in subjectTrees)
            {
                var foundSyntaxes = (
                    from snode in subjectTree.SyntaxRoot.DescendantNodes()
                    where snode is TypeDeclarationSyntax || snode is EnumDeclarationSyntax || snode is DelegateDeclarationSyntax
                    select snode
                    ).ToList();

                foreach (var foundTypeSyntax in foundSyntaxes)
                {
                    var symbolInfo = (INamedTypeSymbol?)subjectTree.SemanticModel.GetDeclaredSymbol(foundTypeSyntax);
                    if (symbolInfo == null)
                    {
                        //skip this type
                        continue;
                    }

                    if (!processedTypeNames.Add(symbolInfo.ToDisplayString()))
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

                    if (!ntc.TransitionDict.TryGetValue(symbolNamespace, out var targetNamespaceInfo))
                    {
                        //there is no transition for this namespace: the type is declared
                        //outside of any namespace, for example. Nothing to move.
                        continue;
                    }

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
                    _namespaceCenter.TypeAdded(symbolInfo, targetNamespaceInfo.ModifiedName);
                }
            }
        }

        /// <summary>
        /// A syntax tree of the subject file with its semantic model.
        /// </summary>
        private readonly struct SubjectTree
        {
            public readonly SyntaxNode SyntaxRoot;

            public readonly SemanticModel SemanticModel;

            public SubjectTree(
                SyntaxNode syntaxRoot,
                SemanticModel semanticModel
                )
            {
                SyntaxRoot = syntaxRoot;
                SemanticModel = semanticModel;
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
                var sourceNamespace = processedType.ContainingNamespace.ToDisplayString();

                if (!ntc.TransitionDict.TryGetValue(sourceNamespace, out var targetNamespaceInfo))
                {
                    //no transition for this namespace, see FixReferencesAsync
                    continue;
                }

                result = result.MoveObject(
                    sourceNamespace,
                    processedType.Name,
                    targetNamespaceInfo.ModifiedName
                    );
            }

            return result;
        }
    }
}
