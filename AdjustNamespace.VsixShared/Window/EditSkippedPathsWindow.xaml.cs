using AdjustNamespace.Helper;
using Microsoft.VisualStudio.PlatformUI;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace AdjustNamespace.Window
{
    /// <summary>
    /// Interaction logic for EditSkippedPathsWindow.xaml
    ///
    /// The editor of the folder list which must not take a part in the target namespace.
    /// The list is stored in the settings file of the solution, see
    /// <see cref="Settings.AdjustNamespaceSettings.SkippedFolderSuffixes"/>.
    /// </summary>
    public partial class EditSkippedPathsWindow : DialogWindow
    {
        private readonly VsServices _vss;
        private readonly string _solutionFolder;

        /// <param name="vss">Visual Studio services.</param>
        public EditSkippedPathsWindow(
            VsServices vss
            )
        {
            _vss = vss;
            _solutionFolder = new FileInfo(_vss.Workspace.CurrentSolution.FilePath).Directory.FullName;

            InitializeComponent();

            foreach (var skipped in _vss.Settings.Settings.SkippedFolderSuffixes)
            {
                this.PathList.Items.Add(
                    new ItemViewModel(Path.IsPathRooted(skipped), skipped)
                    );
            }
        }

        /// <summary>
        /// Ask the user for a folder and add it into the list (the duplicates are ignored).
        /// </summary>
        public void Add_Click(object sender, RoutedEventArgs e)
        {
            using (var w = new FolderBrowserDialog())
            {
                w.SelectedPath = _solutionFolder;
                w.ShowNewFolderButton = false;

                if (w.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                (bool pathRooted, string fpath) = Determine(w);

                //check for duplicates
                foreach (ItemViewModel skipped in this.PathList.Items)
                {
                    if (skipped.Suffix == fpath)
                    {
                        return;
                    }
                }

                this.PathList.Items.Add(
                    new ItemViewModel(pathRooted, fpath)
                    );
            }
        }

        /// <summary>
        /// Ask the user for a new folder for the selected item of the list.
        /// </summary>
        public void Edit_Click(object sender, RoutedEventArgs e)
        {
            var selectvm = this.PathList.SelectedItem as ItemViewModel;
            if (selectvm is null)
            {
                return;
            }

            using (var w = new FolderBrowserDialog())
            {
                w.SelectedPath =
                    selectvm.IsPathRooted
                    ? selectvm.Suffix
                    : Path.Combine(_solutionFolder, selectvm.Suffix);

                w.ShowNewFolderButton = false;

                if (w.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                (bool pathRooted, string fpath) = Determine(w);

                //remove the edition one
                var index = this.PathList.Items.IndexOf(selectvm);
                this.PathList.Items.Remove(selectvm);

                //check for duplicates
                foreach (ItemViewModel skipped in this.PathList.Items)
                {
                    if (skipped.Suffix == fpath)
                    {
                        return;
                    }
                }

                this.PathList.Items.Insert(
                    index,
                    new ItemViewModel(pathRooted, fpath)
                    );
            }
        }

        /// <summary>
        /// Convert the chosen folder into a path relative to the solution folder
        /// (so the settings file can be shared across the team); a folder outside
        /// of the solution folder is stored as a rooted path.
        /// </summary>
        private (bool pathRooted, string fpath) Determine(FolderBrowserDialog w)
        {
            var pathRooted = true;
            var fpath = w.SelectedPath;
            if (w.SelectedPath.StartsWith(_solutionFolder) && w.SelectedPath.Length >= (_solutionFolder.Length + 2))
            {
                //trim if the selected path is in subfolder relative to the sln
                pathRooted = false;
                fpath = w.SelectedPath.Substring(_solutionFolder.Length + 1);
            }

            return (pathRooted, fpath);
        }

        /// <summary>
        /// Remove the selected item from the list.
        /// </summary>
        public void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectvm = this.PathList.SelectedItem as ItemViewModel;
            if (selectvm is null)
            {
                return;
            }

            this.PathList.Items.Remove(selectvm);
        }

        /// <summary>
        /// Write the list into the settings file of the solution and close the window.
        /// </summary>
        public void Save_Click(object sender, RoutedEventArgs e)
        {
            _vss.Settings.Settings.SkippedFolderSuffixes.Clear();
            foreach (ItemViewModel skipped in this.PathList.Items)
            {
                _vss.Settings.Settings.SkippedFolderSuffixes.Add(
                    skipped.Suffix
                    );
            }

            _vss.SettingsReader.Save(_vss.Settings.Settings);

            this.Close();
        }

        /// <summary>
        /// Close the window without saving.
        /// </summary>
        public void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// An item of the skipped folder list.
    /// </summary>
    public sealed class ItemViewModel : BaseViewModel
    {
        /// <summary>
        /// The path is a rooted one (i.e. it is not relative to the solution folder).
        /// </summary>
        public bool IsPathRooted
        {
            get;
        }

        /// <summary>
        /// The path itself, as it is stored in the settings file.
        /// </summary>
        public string Suffix
        {
            get;
        }

        public ItemViewModel(bool isPathRooted, string suffix)
        {
            IsPathRooted = isPathRooted;
            Suffix = suffix;
        }
    }
}
