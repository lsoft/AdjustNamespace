using System;
using System.Collections.Generic;
using System.Text;

namespace AdjustNamespace.UI.ViewModel.Select
{
    /// <summary>
    /// An item of the list shown on the second wizard step:
    /// either a folder (<see cref="SelectFolderViewModel"/>)
    /// or a file (<see cref="SelectFileViewModel"/>).
    /// </summary>
    public interface ISelectItemViewModel
    {
        /// <summary>
        /// The file behind this item; <c>null</c> for a folder item.
        /// </summary>
        FileEx? FileEx
        {
            get;
        }

        /// <summary>
        /// The item is chosen for the adjusting.
        /// <c>null</c> means the middle state (a folder whose files are partially checked).
        /// </summary>
        bool? IsChecked
        {
            get;
            set;
        }

        /// <summary>
        /// The item is selected (highlighted) in the list.
        /// </summary>
        bool IsSelected
        {
            get;
            set;
        }

        /// <summary>
        /// Text shown to the user: a folder path or a file name.
        /// </summary>
        string ItemPath
        {
            get;
        }

        /// <summary>
        /// Clear the references to the parent.
        /// </summary>
        void Clear();
    }
}
