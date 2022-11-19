using AdjustNamespace.Adjusting;
using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Helper;
using AdjustNamespace.Options;
using EnvDTE80;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
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
            List<string> subjectFilePaths
            )
        {
            if (formCloser is null)
            {
                throw new ArgumentNullException(nameof(formCloser));
            }

            if (subjectFilePaths is null)
            {
                throw new ArgumentNullException(nameof(subjectFilePaths));
            }

            _vss = vss;
            _formCloser = formCloser;
            _subjectFilePaths = subjectFilePaths;
            _progressMessage = string.Empty;
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            _task = PerformAdjustingAsync(_cts.Token);
            await _task;

            if (_cts.IsCancellationRequested)
            {
                ProgressMessage = $"Cancelled";
            }
            else
            {
                ProgressMessage = $"Completed";
            }

            await System.Threading.Tasks.Task.Delay(750);

            GeneralOptions.Instance.FilesAdjusted += _subjectFilePaths.Count;

            _cts.Dispose();
            _formCloser();
        }

        private async System.Threading.Tasks.Task PerformAdjustingAsync(CancellationToken cancellationToken)
        {
            var adjusterFactory = await AdjusterFactory.CreateAsync(_vss);

            //process file by file
            for (var i = 0; i < _subjectFilePaths.Count; i++)
            {
                if(cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var subjectFilePath = _subjectFilePaths[i];

                ProgressMessage = $"{i + 1}/{_subjectFilePaths.Count}: {subjectFilePath}";
                Debug.WriteLine($"----------------------------> {i} {subjectFilePath}");

                var adjuster = adjusterFactory.Create(subjectFilePath);
                if (adjuster is not null)
                {
                    await adjuster.AdjustAsync();
                }
            }
        }
    }
}

