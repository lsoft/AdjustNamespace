using AdjustNamespace.Roslyn;
using AdjustNamespace.Xaml;
using AdjustNamespace.Xaml.BodyProvider;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdjustNamespace.Adjusting.Edit;
using AdjustNamespace.Adjusting.Edit.Apply;
using AdjustNamespace.Namespace;
using AdjustNamespace.Adjusting.Adjuster.Cs;
using AdjustNamespace.Adjusting.Plan;
using AdjustNamespace;

namespace AdjustNamespace.Adjusting.Adjuster
{
    /// <summary>
    /// Adjuster for cs file.
    ///
    /// The workflow is:
    /// 1) find every reference to the types declared in this file and schedule an edit for each
    ///    of them (a new using clause or an edited fully qualified name), see <see cref="RefProcessor"/>;
    /// 2) schedule the move of the namespace declarations of the file itself;
    /// 3) apply all the scheduled edits at once, see <see cref="EditApplier"/>;
    /// 4) fix the references to the moved types in the xaml files of the solution.
    ///
    /// Whether this file is a subject to change at all and which namespace transitions
    /// (old namespace -> new namespace) it has, has been decided by <see cref="AdjustPlanner"/>
    /// already: this class performs the plan and does not question it.
    /// </summary>
    public class CsAdjuster : IAdjuster
    {
        private readonly Workspace _workspace;
        private readonly IXamlBodyProviderFactory _xamlBodyProviderFactory;
        private readonly NamespaceCenter _namespaceCenter;
        private readonly string _subjectFilePath;
        private readonly string _targetNamespace;
        private readonly NamespaceTransitionContainer _transitions;
        private readonly List<string> _xamlFilePaths;

        /// <param name="workspace">Roslyn workspace of the solution.</param>
        /// <param name="xamlBodyProviderFactory">How a xaml file is read and written.</param>
        /// <param name="namespaceCenter">Namespace state container shared across the whole adjusting session.</param>
        /// <param name="plan">What has to happen with the file.</param>
        /// <param name="xamlFilePaths">All the xaml files of the solution (they may reference the moved types).</param>
        public CsAdjuster(
            Workspace workspace,
            IXamlBodyProviderFactory xamlBodyProviderFactory,
            NamespaceCenter namespaceCenter,
            AdjustPlanItem plan,
            List<string> xamlFilePaths
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (xamlBodyProviderFactory is null)
            {
                throw new ArgumentNullException(nameof(xamlBodyProviderFactory));
            }

            if (namespaceCenter is null)
            {
                throw new ArgumentNullException(nameof(namespaceCenter));
            }

            if (xamlFilePaths is null)
            {
                throw new ArgumentNullException(nameof(xamlFilePaths));
            }

            _workspace = workspace;
            _xamlBodyProviderFactory = xamlBodyProviderFactory;
            _namespaceCenter = namespaceCenter;
            _subjectFilePath = plan.FilePath;
            _targetNamespace = plan.TargetNamespace;
            _transitions = plan.Transitions;
            _xamlFilePaths = xamlFilePaths;
        }

        /// <inheritdoc/>
        public async Task<bool> AdjustAsync(CancellationToken cancellationToken = default)
        {
            AdjustLog.WriteLine($"[Adjust] CsAdjuster: {_subjectFilePath} -> {_targetNamespace}");

            //a file may be compiled by several projects (the target frameworks of a multi target
            //project), and every one of them parses it with its own conditional compilation
            //symbols: a type which is declared under such a symbol exists in a part of these
            //trees only, so all of them are taken into account
            var subjectTrees = await GetSubjectTreesAsync(cancellationToken);
            if (subjectTrees.Count == 0)
            {
                AdjustLog.WriteLine($"[Adjust] CsAdjuster: {_subjectFilePath} has no semantic model, skipped ({subjectTrees.Count} tree(s))");

                //there is no semantic model for this file, so there is nothing we can do
                return false;
            }

            AdjustLog.WriteLine($"[Adjust] CsAdjuster: {_subjectFilePath} compiled by {subjectTrees.Count} project(s)");

            var edits = new EditSet();

            var processedTypes = new Dictionary<INamedTypeSymbol, NamespaceTransition>(SymbolEqualityComparer.Default);

            //fix refs (adding a new using namespace clauses or edit fully qualified names)
            await FixReferencesAsync(
                processedTypes,
                subjectTrees,
                edits,
                cancellationToken
                );

            //fix the references the file itself makes to types of its old enclosing namespace
            FixSelfReferences(
                subjectTrees,
                edits
                );

            //move the namespaces of the current file; only the root ones are moved, the nested
            //ones follow them automatically because their full name contains the root one
            foreach (var transition in _transitions.Transitions.Where(t => t.IsRoot))
            {
                edits.MoveNamespace(_subjectFilePath, transition);
            }

            //perform the changes. The cancellation is not asked anymore: the edits of one file
            //describe a single consistent change of the solution and a half of them is a broken
            //file, so the writing is never interrupted
            await new EditApplier(_workspace)
                .ApplyAsync(edits);

            //TODO: describe these changes as the edits of the set above
            await FixReferenceInXamlFilesAsync(
                processedTypes
                );

            return true;
        }

        /// <summary>
        /// The documents the subject file is compiled as, with their syntax trees and semantic
        /// models: one per project which compiles it, see <see cref="Roslyn.WorkspaceExtensions.GetDocuments"/>.
        /// </summary>
        private async Task<List<SubjectTree>> GetSubjectTreesAsync(
            CancellationToken cancellationToken
            )
        {
            var result = new List<SubjectTree>();

            foreach (var document in _workspace.GetDocuments(_subjectFilePath))
            {
                var syntaxRoot = await document.GetSyntaxRootAsync(cancellationToken);
                if (syntaxRoot == null)
                {
                    //skip this document
                    continue;
                }

                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
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
        /// Schedule the edits for every reference to every type declared in the subject file.
        /// </summary>
        /// <param name="processedTypes">
        /// (in/out) The types which have been moved, with the transition of each of them.
        /// It is filled here and reused later to fix the references in the xaml files.
        /// </param>
        /// <param name="subjectTrees">The syntax trees of the subject file, one per project which compiles it.</param>
        /// <param name="edits">(out) Set the scheduled edits are placed into.</param>
        /// <param name="cancellationToken">Cancellation of the session.</param>
        private async Task FixReferencesAsync(
            Dictionary<INamedTypeSymbol, NamespaceTransition> processedTypes,
            List<SubjectTree> subjectTrees,
            EditSet edits,
            CancellationToken cancellationToken
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
                    //the reference search of the types below is the longest part of the whole
                    //session, so a cancel is answered between the types and not after the file
                    cancellationToken.ThrowIfCancellationRequested();

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
                        AdjustLog.WriteLine($"[Adjust] Type {symbolInfo.ToDisplayString()}: already in target namespace {_targetNamespace}, skipped");
                        continue;
                    }

                    if (NamespaceHelper.IsSpecialNamespace(symbolNamespace))
                    {
                        AdjustLog.WriteLine($"[Adjust] Type {symbolInfo.ToDisplayString()}: namespace {symbolNamespace} is special, skipped");
                        continue;
                    }

                    //the transition of the very declaration this type is written in and not
                    //the transition of its namespace: one namespace may have several of them,
                    //see NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf
                    var transition = NamespaceTransitionContainer.TryGetTransitionOfTheDeclarationOf(
                        foundTypeSyntax,
                        _targetNamespace
                        );
                    if (!transition.HasValue)
                    {
                        AdjustLog.WriteLine($"[Adjust] Type {symbolInfo.ToDisplayString()}: no namespace transition found, skipped");

                        //there is no transition for this type: it is declared outside of any
                        //namespace, for example. Nothing to move.
                        continue;
                    }

                    var targetNamespaceInfo = transition.Value;

                    if (symbolNamespace == targetNamespaceInfo.ModifiedName)
                    {
                        //current symbol is in target namespace already
                        continue;
                    }

                    AdjustLog.WriteLine($"[Adjust] Type {symbolInfo.ToDisplayString()}: {symbolNamespace} -> {targetNamespaceInfo.ModifiedName}, searching references");

                    //schedule the edits for all references
                    var refProcessor = new RefProcessor(_workspace, edits, targetNamespaceInfo);
                    await refProcessor.ProcessRefsAsync(symbolInfo, cancellationToken);

                    processedTypes[symbolInfo] = targetNamespaceInfo;
                    _namespaceCenter.TypeRemoved(symbolInfo);
                    _namespaceCenter.TypeAdded(symbolInfo, targetNamespaceInfo.ModifiedName);
                }
            }
        }

        /// <summary>
        /// Schedule a <c>using</c> clause for every reference the subject file makes to a type
        /// of its old enclosing namespace, see <see cref="SelfReferenceFixer"/>.
        /// </summary>
        private void FixSelfReferences(
            List<SubjectTree> subjectTrees,
            EditSet edits
            )
        {
            var fixer = new SelfReferenceFixer(edits, _subjectFilePath, _targetNamespace);

            foreach (var subjectTree in subjectTrees)
            {
                fixer.Fix(subjectTree.SyntaxRoot, subjectTree.SemanticModel);
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
        /// To keep it fast, the changes are firstly applied to an in-memory copy of the file
        /// (read from the disk), and only if that copy differs from the original one the
        /// write path (an invisible text buffer in Visual Studio) is touched.
        /// </summary>
        private async System.Threading.Tasks.Task FixReferenceInXamlFilesAsync(
            Dictionary<INamedTypeSymbol, NamespaceTransition> processedTypes
            )
        {
            if (processedTypes is null)
            {
                throw new ArgumentNullException(nameof(processedTypes));
            }

            foreach (var xamlFilePath in _xamlFilePaths)
            {
                if (!XamlPathHelper.IsXamlFile(xamlFilePath))
                {
                    continue;
                }

                var xamlEngine = new XamlEngine(_xamlBodyProviderFactory);

                using var testDocument = await xamlEngine.CreateForReadAsync(xamlFilePath);

                var modifiedTestDocument = PerformChanges(
                    testDocument,
                    processedTypes
                    );

                if (!modifiedTestDocument.IsChangesExists(testDocument))
                {
                    continue;
                }

                using var realDocument = await xamlEngine.CreateForWriteAsync(xamlFilePath);

                var modifiedRealDocument = PerformChanges(
                    realDocument,
                    processedTypes
                    );

                modifiedRealDocument.SaveIfChangesExistsAgainst(realDocument);
            }
        }

        /// <summary>
        /// Apply the namespace transitions of every moved type to the given xaml document.
        /// <see cref="XamlDocument"/> is immutable, hence a new document is returned.
        /// </summary>
        private XamlDocument PerformChanges(
            XamlDocument document,
            Dictionary<INamedTypeSymbol, NamespaceTransition> processedTypes
            )
        {
            var result = document;

            foreach (var pair in processedTypes)
            {
                result = result.MoveObject(
                    pair.Key.ContainingNamespace.ToDisplayString(),
                    pair.Key.Name,
                    pair.Value.ModifiedName
                    );
            }

            return result;
        }
    }
}
