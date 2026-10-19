using AdjustNamespace.Adjusting.Report;
using AdjustNamespace.TextReferences;
using AdjustNamespace.UI.StepFactory;
using AdjustNamespace.VisualStudio;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace AdjustNamespace.UI.ViewModel.Review
{
    /// <summary>
    /// Viewmodel of the review step, the one between the selection and the run: the text
    /// references the search has found, grouped by file, each with a check box the mode has
    /// preset. The matches the user leaves checked are changed by the run.
    ///
    /// The step is there only when the search has found something to show.
    /// </summary>
    public class ReviewStepViewModel : ChainViewModel
    {
        private readonly AdjustContext _context;
        private readonly IWizardHost _host;
        private readonly IStepFactory<PerformingParameters> _nextStepFactory;
        private readonly Func<IStepFactory<SelectedStepParameters>> _previousStepFactory;
        private readonly ReviewParameters _parameters;

        private readonly List<ReviewMatchViewModel> _rows = new();

        private bool _showCertain = true;
        private bool _showLikely = true;
        private bool _showPossible = true;
        private bool _showUnlikely;

        private ICommand? _nextCommand;
        private ICommand? _previousCommand;
        private ICommand? _cancelCommand;
        private ICommand? _checkLikelyCommand;
        private ICommand? _checkAllCommand;
        private ICommand? _uncheckAllCommand;
        private ICommand? _openCommand;

        /// <summary>
        /// The files with the text references that pass the filter.
        /// </summary>
        public ObservableCollection<ReviewFileViewModel> Files
        {
            get;
        } = new ObservableCollection<ReviewFileViewModel>();

        /// <summary>
        /// The summary line: how many text references there are and how many are checked.
        /// </summary>
        public string Summary
        {
            get
            {
                var all = _parameters.TextReferences.Shown.Count();
                var checkedCount = _parameters.TextReferences.Approved.Count();

                return $"{all} text reference(s) to the moved types found, {checkedCount} checked. "
                    + "The checked ones are changed by the run; double click opens the file.";
            }
        }

        /// <summary>
        /// The mode the search has been performed with, for the user to see why the boxes are
        /// checked the way they are.
        /// </summary>
        public string ModeText => $"Mode: {_parameters.TextReferences.Options.Mode}";

        /// <summary>
        /// Show the matches of the level Certain.
        /// </summary>
        public bool ShowCertain
        {
            get => _showCertain;
            set
            {
                _showCertain = value;
                Rebuild();
            }
        }

        /// <summary>
        /// Show the matches of the level Likely.
        /// </summary>
        public bool ShowLikely
        {
            get => _showLikely;
            set
            {
                _showLikely = value;
                Rebuild();
            }
        }

        /// <summary>
        /// Show the matches of the level Possible.
        /// </summary>
        public bool ShowPossible
        {
            get => _showPossible;
            set
            {
                _showPossible = value;
                Rebuild();
            }
        }

        /// <summary>
        /// Show the matches of the level Unlikely, which are hidden by default.
        /// </summary>
        public bool ShowUnlikely
        {
            get => _showUnlikely;
            set
            {
                _showUnlikely = value;
                Rebuild();
            }
        }

        /// <summary>
        /// Check every match of the level Likely and above.
        /// </summary>
        public ICommand CheckLikelyCommand
        {
            get
            {
                if (_checkLikelyCommand == null)
                {
                    _checkLikelyCommand = new RelayCommand(a => SetChecked(m => m.Level >= ConfidenceLevel.Likely, true));
                }

                return _checkLikelyCommand;
            }
        }

        /// <summary>
        /// Check every match that is shown.
        /// </summary>
        public ICommand CheckAllCommand
        {
            get
            {
                if (_checkAllCommand == null)
                {
                    _checkAllCommand = new RelayCommand(a => SetChecked(m => true, true));
                }

                return _checkAllCommand;
            }
        }

        /// <summary>
        /// Uncheck every match.
        /// </summary>
        public ICommand UncheckAllCommand
        {
            get
            {
                if (_uncheckAllCommand == null)
                {
                    _uncheckAllCommand = new RelayCommand(a => SetChecked(m => true, false));
                }

                return _uncheckAllCommand;
            }
        }

        /// <summary>
        /// Open the file of a match at its line (a double click).
        /// </summary>
        public ICommand OpenCommand
        {
            get
            {
                if (_openCommand == null)
                {
                    _openCommand = new AsyncRelayCommand(
                        async a =>
                        {
                            if (a is ReviewMatchViewModel row)
                            {
                                await FileNavigator.OpenAsync(row.Match.FilePath, row.Match.Line, row.Match.Column);
                            }
                        });
                }

                return _openCommand;
            }
        }

        /// <summary>
        /// Close the wizard.
        /// </summary>
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    _cancelCommand = new RelayCommand(a => _host.Close());
                }

                return _cancelCommand;
            }
        }

        /// <summary>
        /// Go back to the selection.
        /// </summary>
        public ICommand PreviousCommand
        {
            get
            {
                if (_previousCommand == null)
                {
                    _previousCommand = new AsyncRelayCommand(
                        async a =>
                        {
                            await _previousStepFactory().CreateAsync(
                                new SelectedStepParameters(
                                    _parameters.FilePaths,
                                    _parameters.TextReferences.Options
                                    )
                                );
                        });
                }

                return _previousCommand;
            }
        }

        /// <summary>
        /// Start the adjusting: whatever is checked is changed.
        /// </summary>
        public ICommand NextCommand
        {
            get
            {
                if (_nextCommand == null)
                {
                    _nextCommand = new AsyncRelayCommand(
                        async a =>
                        {
                            await _nextStepFactory.CreateAsync(
                                new PerformingParameters(
                                    _parameters.SubjectFilePaths,
                                    _parameters.ReplaceRegex,
                                    _parameters.Blocked,
                                    _parameters.TextReferences
                                    )
                                );
                        });
                }

                return _nextCommand;
            }
        }

        /// <param name="context">Everything the adjusting session works with.</param>
        /// <param name="host">The window this step is shown in.</param>
        /// <param name="previousStepFactory">Factory of the selection step.</param>
        /// <param name="nextStepFactory">Factory of the performing step.</param>
        /// <param name="parameters">Parameters of this step.</param>
        public ReviewStepViewModel(
            AdjustContext context,
            IWizardHost host,
            Func<IStepFactory<SelectedStepParameters>> previousStepFactory,
            IStepFactory<PerformingParameters> nextStepFactory,
            ReviewParameters parameters
            )
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (previousStepFactory is null)
            {
                throw new ArgumentNullException(nameof(previousStepFactory));
            }

            if (nextStepFactory is null)
            {
                throw new ArgumentNullException(nameof(nextStepFactory));
            }

            _context = context;
            _host = host;
            _previousStepFactory = previousStepFactory;
            _nextStepFactory = nextStepFactory;
            _parameters = parameters;

            //the unlikely matches are there for whoever asks for them, the rest is what the
            //options say is shown
            foreach (var match in parameters.TextReferences.Matches)
            {
                _rows.Add(new ReviewMatchViewModel(match, OnCheckedChanged));
            }

            Rebuild();
        }

        /// <inheritdoc/>
        public override System.Threading.Tasks.Task StartAsync()
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Fill the list with the rows that pass the filter.
        /// </summary>
        private void Rebuild()
        {
            Files.Clear();

            var options = _parameters.TextReferences.Options;

            var groups = _rows
                .Where(r => IsShownByFilter(r.Match.Level))
                .Where(r => r.Match.Level == ConfidenceLevel.Unlikely || options.IsShown(r.Match.Level))
                .GroupBy(r => r.Match.FilePath, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var file = new ReviewFileViewModel(
                    group.Key,
                    ReportPaths.Relative(group.Key, _context.Settings.SolutionFolder)
                    );

                foreach (var row in group)
                {
                    file.Matches.Add(row);
                }

                Files.Add(file);
            }

            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(ShowCertain));
            OnPropertyChanged(nameof(ShowLikely));
            OnPropertyChanged(nameof(ShowPossible));
            OnPropertyChanged(nameof(ShowUnlikely));
        }

        private bool IsShownByFilter(ConfidenceLevel level)
        {
            switch (level)
            {
                case ConfidenceLevel.Certain:
                    return _showCertain;

                case ConfidenceLevel.Likely:
                    return _showLikely;

                case ConfidenceLevel.Possible:
                    return _showPossible;

                default:
                    return _showUnlikely;
            }
        }

        private void SetChecked(Func<TextReferenceMatch, bool> predicate, bool value)
        {
            foreach (var row in _rows)
            {
                if (predicate(row.Match) && (value == false || IsShownByFilter(row.Match.Level)))
                {
                    row.Match.IsApproved = value;
                    row.RefreshChecked();
                }
            }

            OnPropertyChanged(nameof(Summary));
        }

        private void OnCheckedChanged()
        {
            OnPropertyChanged(nameof(Summary));
        }
    }
}
