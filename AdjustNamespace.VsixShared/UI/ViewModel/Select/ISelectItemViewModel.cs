using System;
using System.Collections.Generic;
using System.Text;

namespace AdjustNamespace.UI.ViewModel.Select
{
    public interface ISelectItemViewModel
    {
        FileEx? FileEx
        {
            get;
        }

        bool? IsChecked
        {
            get;
            set;
        }

        bool IsSelected
        {
            get;
            set;
        }

        string ItemPath
        {
            get;
        }
    }
}
