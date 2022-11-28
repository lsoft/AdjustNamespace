using AdjustNamespace.Helper;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace AdjustNamespace.UI.ViewModel.Select
{
    public class SelectFileViewModel : BaseViewModel, ISelectItemViewModel
    {
        private SelectFolderViewModel? _parentViewModel;

        private bool _isChecked; //file cannot be in the middle state
        private bool _isSelected;

        public FileEx? FileEx
        {
            get;
        }

        public FontWeight FontWeight => FontWeights.Regular;

        public Thickness LeftMargin
        {
            get;
        }

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

        public bool IsSelected
        {
            get => _isSelected;
            set => _isSelected = value;
        }

        public string ItemPath
        {
            get;
        }

        public void SetCheckedStatusFromParent(bool isChecked)
        {
            _isChecked = isChecked;
            OnPropertyChanged(nameof(IsChecked));
        }


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

        public void Clear()
        {
            _parentViewModel = null;
        }
    }
}
