using AdjustNamespace.Helper;
using EnvDTE80;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using AdjustNamespace.UI.StepFactory;
using Microsoft.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using System.Windows;
using System.Threading;
using Newtonsoft.Json.Linq;
using AdjustNamespace.UI.ViewModel.Select;

namespace AdjustNamespace.UI.ViewModel
{
    public class SelectedStepViewModel : ChainViewModel
    {
        private readonly VsServices _vss;
        private readonly IStepFactory _nextStepFactory;
        private readonly List<FileEx> _filteredFileExs;

        private Brush _foreground;
        private string _mainMessage;

        private ICommand? _closeCommand;
        private ICommand? _nextCommand;
        private ICommand? _invertStatusCommand;

        public string MainMessage
        {
            get => _mainMessage;
            private set
            {
                _mainMessage = value;
                OnPropertyChanged(nameof(MainMessage));
            }
        }

        public ObservableCollection<ISelectItemViewModel> ToFilterItems
        {
            get;
            private set;
        }

        public Brush Foreground
        {
            get => _foreground;
            private set
            {
                _foreground = value;
                OnPropertyChanged(nameof(Foreground));
            }
        }


        public ICommand CloseCommand
        {
            get
            {
                if (_closeCommand == null)
                {
                    _closeCommand = new RelayCommand(
                        a =>
                        {
                            if (a is DialogWindow w)
                            {
                                w.Close();
                            }
                        }
                        );
                }

                return _closeCommand;
            }
        }

        public ICommand NextCommand
        {
            get
            {
                if (_nextCommand == null)
                {
                    _nextCommand = new AsyncRelayCommand(
                        async a => await _nextStepFactory.CreateAsync(
                            ToFilterItems.Where(s => s.FileEx.HasValue && s.IsChecked.GetValueOrDefault(false)).Select(s => s.FileEx!.Value.FilePath).ToList()
                            ),
                        r => ToFilterItems.Any(s => s.IsChecked.GetValueOrDefault(false))
                        );
                }

                return _nextCommand;
            }
        }


        public ICommand InvertStatusCommand
        {
            get
            {
                if (_invertStatusCommand == null)
                {
                    _invertStatusCommand = new RelayCommand(
                        a =>
                        {
                            var selected = ToFilterItems.Where(i => i.IsSelected).ToList();

                            if (selected.Count == 0)
                            {
                                return;
                            }

                            var newValue = !selected[0].IsChecked.GetValueOrDefault(false);
                            selected.ForEach(s => s.IsChecked = newValue);
                        }
                        );
                }

                return _invertStatusCommand;
            }
        }


        public SelectedStepViewModel(
            VsServices vss,
            IStepFactory nextStepFactory,
            List<FileEx> fileExtensions
            )
        {
            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            if (fileExtensions is null)
            {
                throw new ArgumentNullException(nameof(fileExtensions));
            }
            _vss = vss;
            _nextStepFactory = nextStepFactory;
            _filteredFileExs = fileExtensions;

            _foreground = Brushes.Green;
            _mainMessage = "Choose files to process...";
            ToFilterItems = new ObservableCollection<ISelectItemViewModel>();
        }

        public override async System.Threading.Tasks.Task StartAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            //var dte = await _serviceProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as DTE2;
            //if (dte == null)
            //{
            //    return;
            //}

            //var componentModel = (await _serviceProvider.GetServiceAsync(typeof(SComponentModel)) as IComponentModel)!;
            //if (componentModel == null)
            //{
            //    return;
            //}

            //var workspace = componentModel.GetService<VisualStudioWorkspace>();
            //if (workspace == null)
            //{
            //    return;
            //}

            //perform grouping by files physical folder!
            var dirPaths = _filteredFileExs.Select(f => f.FolderPath).Distinct().ToList();
            var dirs = new List<SelectFolderViewModel>();
            foreach(var dirPath in dirPaths)
            {
                var dir = new SelectFolderViewModel(
                    dirPath
                   );

                var dirFiles = _filteredFileExs
                    .Where(f => f.FolderPath == dirPath)
                    .Select(f => new SelectFileViewModel(f, dir))
                    .ToList();
                dir.AddFiles(dirFiles);

                dirs.Add(dir);
            }

            foreach(var dir in dirs.OrderBy(d => d.ItemPath))
            {
                ToFilterItems.Add(dir);

                foreach (var file in dir.Files)
                {
                    ToFilterItems.Add(file);
                }
            }

            OnPropertyChanged();
        }
    }
}
