using AdjustNamespace.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace AdjustNamespace.UI.ViewModel.Select
{
    /// <summary>
    /// A folder item of the list shown on the second wizard step.
    /// Its checkbox controls the checkboxes of all its files.
    /// </summary>
    public class SelectFolderViewModel : BaseViewModel, ISelectItemViewModel
    {
        private List<SelectFileViewModel> _files;

        private bool _isSelected;

        /// <summary>
        /// The viewmodel of the step. It is cleared in <see cref="Clear"/>
        /// to break the circular reference.
        /// </summary>
        private SelectedStepViewModel? _parent;

        /// <summary>
        /// Files of this folder.
        /// </summary>
        public IReadOnlyList<SelectFileViewModel> Files => _files;

        /// <inheritdoc/>
        public FileEx? FileEx => null;

        /// <summary>
        /// Indent of the item (a folder is shown at the root level).
        /// </summary>
        public Thickness LeftMargin
        {
            get;
        }

        /// <summary>
        /// Font of the item (a folder is shown with the bold one).
        /// </summary>
        public FontWeight FontWeight => FontWeights.Bold;

        /// <inheritdoc/>
        /// <remarks>
        /// The getter aggregates the checkboxes of the files: <c>null</c> means
        /// that they are partially checked. The setter applies the value to all of them.
        /// </remarks>
        public bool? IsChecked
        {
            get
            {
                var q = _files.Select(f => f.IsChecked).Distinct().ToList();
                if (q.Count == 1)
                {
                    return q[0];
                }

                return null;
            }

            set
            {
                foreach (var file in _files)
                {
                    file.SetCheckedStatusFromParent(value.GetValueOrDefault(false));
                }

                _parent?.RefreshStatus();
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        /// <inheritdoc/>
        public bool IsSelected
        {
            get => _isSelected;
            set => _isSelected = value;
        }

        /// <inheritdoc/>
        public string ItemPath
        {
            get;
        }


        /// <param name="parent">Viewmodel of the wizard step.</param>
        /// <param name="folderPath">Full path to the folder.</param>
        public SelectFolderViewModel(
            SelectedStepViewModel parent,
            string folderPath
            )
        {
            if (parent is null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (folderPath is null)
            {
                throw new ArgumentNullException(nameof(folderPath));
            }

            _files = new List<SelectFileViewModel>();

            var level = 0;
            LeftMargin = new Thickness(level * 5, 0, 0, 0);
            _parent = parent;
            ItemPath = folderPath;
            IsChecked = true;
        }

        /// <summary>
        /// Add the files of this folder.
        /// </summary>
        public void AddFiles(
            List<SelectFileViewModel> files
            )
        {
            if (files is null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            _files.AddRange(files);
        }

        /// <summary>
        /// A checkbox of a file has been changed: refresh this folder and the whole step.
        /// </summary>
        public void RefreshStatus()
        {
            _parent?.RefreshStatus();
            OnPropertyChanged();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _parent = null;
        }
    }
}
