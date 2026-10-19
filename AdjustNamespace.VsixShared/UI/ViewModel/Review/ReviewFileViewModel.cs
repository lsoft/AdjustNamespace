using System;
using System.Collections.ObjectModel;

namespace AdjustNamespace.UI.ViewModel.Review
{
    /// <summary>
    /// A file of the review step with the text references found in it.
    /// </summary>
    public sealed class ReviewFileViewModel : BaseViewModel
    {
        private bool _isExpanded = true;

        /// <summary>
        /// Full path to the file.
        /// </summary>
        public string FilePath
        {
            get;
        }

        /// <summary>
        /// The path relative to the folder of the solution.
        /// </summary>
        public string DisplayPath
        {
            get;
        }

        /// <summary>
        /// The text references of the file that pass the filter.
        /// </summary>
        public ObservableCollection<ReviewMatchViewModel> Matches
        {
            get;
        } = new ObservableCollection<ReviewMatchViewModel>();

        /// <summary>
        /// The file is shown expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public ReviewFileViewModel(
            string filePath,
            string displayPath
            )
        {
            if (filePath is null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (displayPath is null)
            {
                throw new ArgumentNullException(nameof(displayPath));
            }

            FilePath = filePath;
            DisplayPath = displayPath;
        }
    }
}
