using AdjustNamespace.Namespace;
using AdjustNamespace.Settings;
using AdjustNamespace.VisualStudio;
using AdjustNamespace.Xaml.BodyProvider;
using Microsoft.CodeAnalysis;
using System;
using System.IO;

namespace AdjustNamespace
{
    /// <summary>
    /// Everything one adjusting session works with.
    ///
    /// It replaces the former <c>VsServices</c>, which carried the raw Visual Studio services
    /// (DTE, the component model, the service provider) and had to be built with four <c>null!</c>
    /// fields for the tests: the boundary between the core and the IDE was not expressed in the
    /// types, so a test which accidentally touched the IDE failed with a
    /// <see cref="NullReferenceException"/> instead of a compilation error.
    ///
    /// Every member here is a real one in the tests as well: the Roslyn workspace is an
    /// <c>AdhocWorkspace</c> and the Visual Studio bound members are interfaces with a fake
    /// behind them. The raw services are gone — the IDE is asked for them only by
    /// <c>VsAdjustContext</c> when the extension builds this context.
    /// </summary>
    public sealed class AdjustContext
    {
        /// <summary>
        /// Roslyn workspace of the currently opened solution.
        /// It is a <c>VisualStudioWorkspace</c> in the real life; the base type is
        /// declared here because nothing in the core needs more than the base API, and this
        /// allows to run the core over an <c>AdhocWorkspace</c> in the tests.
        /// </summary>
        public Workspace Workspace
        {
            get;
        }

        /// <summary>
        /// The files of the solution and the project of every one of them.
        /// </summary>
        public ISolutionExplorer Solution
        {
            get;
        }

        /// <summary>
        /// The target namespace of a file, derived from its path and the solution settings.
        /// </summary>
        public TargetNamespaceResolver TargetNamespaces
        {
            get;
        }

        /// <summary>
        /// How a xaml file is read and written: an invisible text buffer in Visual Studio
        /// (so the change is undoable), the file system elsewhere.
        /// </summary>
        public IXamlBodyProviderFactory XamlBodyProviderFactory
        {
            get;
        }

        public AdjustContext(
            Workspace workspace,
            ISolutionExplorer solution,
            TargetNamespaceResolver targetNamespaces,
            IXamlBodyProviderFactory xamlBodyProviderFactory
            )
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (solution is null)
            {
                throw new ArgumentNullException(nameof(solution));
            }

            if (targetNamespaces is null)
            {
                throw new ArgumentNullException(nameof(targetNamespaces));
            }

            if (xamlBodyProviderFactory is null)
            {
                throw new ArgumentNullException(nameof(xamlBodyProviderFactory));
            }

            Workspace = workspace;
            Solution = solution;
            TargetNamespaces = targetNamespaces;
            XamlBodyProviderFactory = xamlBodyProviderFactory;
        }

        /// <summary>
        /// Read the settings file which lives in the folder of the given solution.
        /// </summary>
        public static AdjustNamespaceSettings2 ReadSettingsOf(
            Workspace workspace
            )
        {
            var solutionFolder = new FileInfo(workspace.CurrentSolution.FilePath!).Directory!.FullName;

            return ReadSettingsOf(solutionFolder);
        }

        /// <summary>
        /// Read the settings file which lives in the given solution folder.
        /// </summary>
        public static AdjustNamespaceSettings2 ReadSettingsOf(
            string solutionFolder
            )
        {
            var reader = new SettingsReader(solutionFolder);

            return new AdjustNamespaceSettings2(
                solutionFolder,
                reader.ReadSettings() ?? new AdjustNamespaceSettings()
                );
        }
    }
}
