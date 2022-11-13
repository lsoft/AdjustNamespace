using AdjustNamespace.Adjusting;
using AdjustNamespace.Adjusting.Adjuster;
using AdjustNamespace.Helper;
using AdjustNamespace.Options;
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
        private readonly VsServices _vss;
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

            var adjusterFactory = await AdjusterFactory.CreateAsync(_vss);

            //process file by file
            for (var i = 0; i < _subjectFilePaths.Count; i++)
            {
                var subjectFilePath = _subjectFilePaths[i];

                ProgressMessage = $"{i + 1}/{_subjectFilePaths.Count}: {subjectFilePath}";
                Debug.WriteLine($"----------------------------> {i} {subjectFilePath}");

                var adjuster = adjusterFactory.Create(subjectFilePath);
                if (adjuster is not null)
                {
                    await adjuster.AdjustAsync();
                }
            }

            ProgressMessage = $"Completed";

            GeneralOptions.Instance.FilesAdjusted += _subjectFilePaths.Count;
            
            _formCloser();
        }
    }
}

