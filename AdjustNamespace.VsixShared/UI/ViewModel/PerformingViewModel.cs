﻿using AdjustNamespace.Adjusting;
using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Helper;
using AdjustNamespace.Options;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AdjustNamespace.UI.ViewModel
{
    public class PerformingViewModel : ChainViewModel
    {
        private readonly CancellationTokenSource _cts = new();

        private readonly VsServices _vss;
        private readonly Action _formCloser;
        private readonly List<string> _subjectFilePaths;
        private readonly NamespaceReplaceRegex _replaceRegex;
        private readonly bool _openFilesToEnableUndo;

        private RelayCommand? _cancelCommand;
        private System.Threading.Tasks.Task? _task;

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

        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    _cancelCommand = new RelayCommand(
                        a =>
                        {
                            if (_task != null && !_cts.IsCancellationRequested)
                            {
                                _cts.Cancel();
                            }
                        },
                        r => !_cts.IsCancellationRequested
                        );
                }

                return _cancelCommand;
            }
        }

        public PerformingViewModel(
            VsServices vss,
            Action formCloser,
            PerformingParameters parameters
            )
        {
            if (formCloser is null)
            {
                throw new ArgumentNullException(nameof(formCloser));
            }

            _vss = vss;
            _formCloser = formCloser;
            _subjectFilePaths = parameters.SubjectFilePaths;
            _replaceRegex = parameters.ReplaceRegex;
            _openFilesToEnableUndo = parameters.OpenFilesToEnableUndo;
            _progressMessage = string.Empty;
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            //var undoManager = (await _vss.ServiceProvider.GetServiceAsync(typeof(SVsLinkedUndoTransactionManager))) as IVsLinkedUndoTransactionManager;
            //ErrorHandler.ThrowOnFailure(
            //    undoManager!.OpenLinkedUndo((uint)LinkedTransactionFlagsEnum.Global, "Adjusting Namespaces")
            //    );

            //int transactionCount = 0;
            //ErrorHandler.ThrowOnFailure(
            //    undoManager.CountOpenTransactions(ref transactionCount)
            //    );

            _task = PerformAdjustingAsync(_cts.Token);
            await _task;

            //ErrorHandler.ThrowOnFailure(undoManager.CloseLinkedUndo());

            if (_cts.IsCancellationRequested)
            {
                ProgressMessage = $"Cancelled";
            }
            else
            {
                ProgressMessage = $"Completed";
            }

            await System.Threading.Tasks.Task.Delay(750);

            General.Instance.FilesAdjusted += _subjectFilePaths.Count;

            _cts.Dispose();
            _formCloser();
        }

        private async System.Threading.Tasks.Task PerformAdjustingAsync(CancellationToken cancellationToken)
        {
            var namespaceCenter = await NamespaceCenter.CreateForAsync(_vss.Workspace);
            var adjusterFactory = await AdjusterFactory.CreateAsync(
                _vss,
                _replaceRegex,
                _openFilesToEnableUndo,
                namespaceCenter
                );
            
            //process file by file
            cancellationToken = await AdjustAsync(adjusterFactory, cancellationToken);

            //cleanup
            await CleanupAsync(namespaceCenter, cancellationToken);
        }

        private async Task<CancellationToken> AdjustAsync(AdjusterFactory adjusterFactory, CancellationToken cancellationToken)
            {
            var total = _subjectFilePaths.Count;
            for (var i = 0; i < total; i++)
                {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var subjectFilePath = _subjectFilePaths[i];

                ProgressMessage = $"{i + 1}/{total}: {subjectFilePath}";
                Debug.WriteLine($"----------------------------> {i} Adjust {subjectFilePath}");

                var adjuster = await adjusterFactory.CreateAsync(subjectFilePath);
                if (adjuster is not null)
                {
                    await adjuster.AdjustAsync();
                }
            }

            return cancellationToken;
        }

        private async Task CleanupAsync(NamespaceCenter namespaceCenter, CancellationToken cancellationToken)
        {
            var cleanupDocuments = _vss.Workspace.EnumerateAllDocumentFilePaths(Predicate.IsProjectInScope, Predicate.IsDocumentInScope).ToList();
            var total = cleanupDocuments.Count;
            var c = new Cleanup(_vss, namespaceCenter);
            for (int i = 0; i < total; i++)
            {
                string documentFilePath = cleanupDocuments[i];
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                ProgressMessage = $"{i + 1}/{total} Performing cleanup {documentFilePath}";
                Debug.WriteLine($"----------------------------> {i} Cleanup {documentFilePath}");

                await c.RemoveEmptyUsingStatementsForAsync(documentFilePath);
            }
        }
    }
    
}
