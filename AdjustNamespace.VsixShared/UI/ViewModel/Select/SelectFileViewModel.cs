using AdjustNamespace.Helper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace AdjustNamespace.UI.ViewModel.Select
{
    /// <summary>
    /// A file item of the list shown on the second wizard step.
    /// </summary>
    public class SelectFileViewModel : BaseViewModel, ISelectItemViewModel
    {
        /// <summary>
        /// The folder item this file belongs to.
        /// It is cleared in <see cref="Clear"/> to break the circular reference.
        /// </summary>
        private SelectFolderViewModel? _parentViewModel;

        private bool _isChecked; //file cannot be in the middle state
        private bool _isSelected;

        /// <inheritdoc/>
        public FileEx? FileEx
        {
            get;
        }

        /// <summary>
        /// Font of the item (a file is shown with the regular one).
        /// </summary>
        public FontWeight FontWeight => FontWeights.Regular;

        /// <summary>
        /// Indent of the item (a file is shown under its folder).
        /// </summary>
        public Thickness LeftMargin
        {
            get;
        }

        /// <inheritdoc/>
        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value.GetValueOrDefault(false);
                OnPropertyChanged(nameof(IsChecked));
                _parentViewModel?.RefreshStatus();
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

        /// <summary>
        /// Set the checkbox from the folder item without notifying that folder back
        /// (otherwise the notification would bounce between the folder and its files).
        /// </summary>
        public void SetCheckedStatusFromParent(bool isChecked)
        {
            _isChecked = isChecked;
            OnPropertyChanged(nameof(IsChecked));
        }


        /// <param name="fileEx">The file behind this item.</param>
        /// <param name="parentViewModel">The folder item this file belongs to.</param>
        public SelectFileViewModel(
            FileEx fileEx,
            SelectFolderViewModel parentViewModel
            )
        {
            if (parentViewModel is null)
            {
                throw new ArgumentNullException(nameof(parentViewModel));
            }

            FileEx = fileEx;

            var level = 2;
            LeftMargin = new Thickness(level * 5, 0, 0, 0);
            ItemPath = fileEx.FileName;
            _parentViewModel = parentViewModel;
            IsChecked = true;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _parentViewModel = null;
        }
    }
}
