using AdjustNamespace.Adjusting;
using AdjustNamespace.Helper;
using AdjustNamespace.Mover;
using AdjustNamespace.Xaml;
using EnvDTE80;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;

namespace AdjustNamespace.ViewModel
{
    public class PerformingViewModel : ChainViewModel
    {
        private readonly IChainMoverState _moverState;
        private readonly List<string> _subjectFilePaths;

        private string _progressMessage;

        public string ProgressMessage
        {
            get => _progressMessage;
            private set
            {
                _progressMessage = value;
                OnPropertyChanged(nameof(ProgressMessage));
            }
        }

        public PerformingViewModel(
            IChainMoverState moverState,
            Dispatcher dispatcher,
            List<string> subjectFilePaths
            )
            : base(dispatcher)
        {
            if (moverState is null)
            {
                throw new ArgumentNullException(nameof(moverState));
            }

            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            _moverState = moverState;
            _subjectFilePaths = subjectFilePaths;
            _progressMessage = string.Empty;
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await _moverState.ServiceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as DTE2;
            if (dte == null)
            {
                return;
            }

            var componentModel = (IComponentModel)await _moverState.ServiceProvider.GetServiceAsync(typeof(SComponentModel));
            if (componentModel == null)
            {
                return;
            }

            var workspace = componentModel.GetService<VisualStudioWorkspace>();
            if (workspace == null)
            {
                return;
            }

            var namespaceCenter = new NamespaceCenter(
                (await workspace.GetAllTypesInNamespaceRecursivelyAsync()).Values
                );

            #region get all xaml files in current solution

            var filePaths = dte.Solution.ProcessSolution();
            var xamlFilePaths = filePaths.FindAll(fp => fp.EndsWith(".xaml"));

            #endregion

            for (var i = 0; i < _subjectFilePaths.Count; i++)
            {
                var subjectFilePath = _subjectFilePaths[i];

                ProgressMessage = $"{i + 1}/{_subjectFilePaths.Count}: {subjectFilePath}";
                Debug.WriteLine($"----------------------------> {i} {subjectFilePath}");

                #region build target namespace

                var subjectProjectItem = dte.Solution.TryGetProjectItem(subjectFilePath);
                if (subjectProjectItem == null)
                {
                    continue;
                }

                var roslynProject = workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == subjectProjectItem.ContainingProject.FullName);
                if (roslynProject == null)
                {
                    continue;
                }

                #endregion

                if (subjectFilePath.EndsWith(".xaml"))
                {
                    //it's a xaml

                    var targetNamespace = roslynProject.GetTargetNamespace(subjectFilePath);

                    var xamlAdjuster = new XamlAdjuster(subjectFilePath, targetNamespace);

                    xamlAdjuster.Adjust();
                }
                else
                {
                    //we can do nothing with not a C# documents
                    var subjectDocument = workspace.GetDocument(subjectFilePath);
                    if (!subjectDocument.IsDocumentInScope())
                    {
                        continue;
                    }

                    var targetNamespace = roslynProject.GetTargetNamespace(subjectFilePath);

                    var csAdjuster = new CsAdjuster(
                        workspace,
                        namespaceCenter,
                        subjectFilePath,
                        targetNamespace,
                        xamlFilePaths
                        );

                    await csAdjuster.AdjustAsync();
                }
            }

            ProgressMessage = $"Completed";
        }
    }
}

