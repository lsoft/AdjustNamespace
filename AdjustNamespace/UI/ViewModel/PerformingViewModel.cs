using AdjustNamespace.Adjusting;
using AdjustNamespace.Helper;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace AdjustNamespace.UI.ViewModel
{
    public class PerformingViewModel : ChainViewModel
    {
        private readonly IAsyncServiceProvider _serviceProvider;
        private readonly Action _formCloser;
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
            IAsyncServiceProvider serviceProvider,
            Action formCloser,
            List<string> subjectFilePaths
            )
        {
            if (serviceProvider is null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (formCloser is null)
            {
                throw new ArgumentNullException(nameof(formCloser));
            }

            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            _serviceProvider = serviceProvider;
            _formCloser = formCloser;
            _subjectFilePaths = subjectFilePaths;
            _progressMessage = string.Empty;
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await _serviceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as DTE2;
            if (dte == null)
            {
                return;
            }

            var componentModel = (IComponentModel)await _serviceProvider.GetServiceAsync(typeof(SComponentModel));
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

                if (!dte.Solution.TryGetProjectItem(subjectFilePath, out var subjectProject, out var subjectProjectItem))
                {
                    continue;
                }

                var roslynProject = workspace.CurrentSolution.Projects.FirstOrDefault(p => p.FilePath == subjectProjectItem!.ContainingProject.FullName);
                if (roslynProject == null)
                {
                    continue;
                }

                #endregion

                if (subjectFilePath.EndsWith(".xaml"))
                {
                    //it's a xaml

                    if (roslynProject.TryGetTargetNamespace(subjectFilePath, out var targetNamespace))
                    {
                        var xamlAdjuster = new XamlAdjuster(subjectFilePath, targetNamespace!);
                        xamlAdjuster.Adjust();
                    }
                }
                else
                {
                    //we can do nothing with not a C# documents
                    var subjectDocument = workspace.GetDocument(subjectFilePath);
                    if (!subjectDocument.IsDocumentInScope())
                    {
                        continue;
                    }

                    if (roslynProject.TryGetTargetNamespace(subjectFilePath, out var targetNamespace))
                    {
                        var csAdjuster = new CsAdjuster(
                            workspace,
                            namespaceCenter,
                            subjectFilePath,
                            targetNamespace!,
                            xamlFilePaths
                            );

                        await csAdjuster.AdjustAsync();
                    }
                }
            }

            ProgressMessage = $"Completed";
            _formCloser();
        }
    }
}

