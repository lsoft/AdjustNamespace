using AdjustNamespace.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace AdjustNamespace.UI.ViewModel.Select
{
    public class SelectFolderViewModel : BaseViewModel, ISelectItemViewModel
    {
        private List<SelectFileViewModel> _files;

        private bool _isSelected;

        public IReadOnlyList<SelectFileViewModel> Files => _files;

        public FileEx? FileEx => null;

        public Thickness LeftMargin
        {
            get;
        }

        public FontWeight FontWeight => FontWeights.Bold;

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
                //if (value == null)
                //{
                //    return;
                //}

                foreach (var file in _files)
                {
                    file.SetCheckedStatusFromParent(value.GetValueOrDefault(false));
                }

                OnPropertyChanged(nameof(IsChecked));
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


        public SelectFolderViewModel(
            string folderPath
            )
        {
            if (folderPath is null)
            {
                throw new ArgumentNullException(nameof(folderPath));
            }

            _files = new List<SelectFileViewModel>();

            var level = 0;
            LeftMargin = new Thickness(level * 5, 0, 0, 0);
            ItemPath = folderPath;
            IsChecked = true;
        }

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

        public void RefreshStatus()
        {
            OnPropertyChanged();
        }
    }
}
