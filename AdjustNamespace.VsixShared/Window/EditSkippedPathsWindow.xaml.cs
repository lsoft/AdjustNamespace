using AdjustNamespace.Helper;
using AdjustNamespace.Options;
using AdjustNamespace.UI.StepFactory;
using AdjustNamespace.VsixShared.Settings;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime;
using System.Windows;
using System.Windows.Forms;
using static AdjustNamespace.Options.DialogPageProvider;
using Task = System.Threading.Tasks.Task;

namespace AdjustNamespace.Window
{
    /// <summary>
    /// Interaction logic for EditSkippedPathsWindow.xaml
    /// </summary>
    public partial class EditSkippedPathsWindow : DialogWindow
    {
        private readonly VsServices _vss;
        private readonly string _solutionFolder;

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

        public void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectvm = this.PathList.SelectedItem as ItemViewModel;
            if (selectvm is null)
            {
                return;
            }

            this.PathList.Items.Remove(selectvm);
        }

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

        public void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public sealed class ItemViewModel : BaseViewModel
    {
        public bool IsPathRooted
        {
            get;
        }

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
