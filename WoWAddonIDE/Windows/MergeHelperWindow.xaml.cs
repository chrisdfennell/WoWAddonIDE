using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class MergeHelperWindow : Window
    {
        private readonly string _repoPath;
        private List<GitService.ConflictTriplet> _conflicts = new();
        private int _index = -1;

        // Use this ctor from MainWindow: new MergeHelperWindow(projectRoot).ShowDialog();
        public MergeHelperWindow(string repoPath)
        {
            _repoPath = repoPath ?? throw new ArgumentNullException(nameof(repoPath));
            InitializeComponent();
            Loaded += MergeHelperWindow_Loaded;
        }

        // Optional parameterless ctor if you prefer setting RepoPath later via SetRepoPath.
        public MergeHelperWindow()
        {
            _repoPath = "";
            InitializeComponent();
            Loaded += MergeHelperWindow_Loaded;
        }

        public void SetRepoPath(string repoPath)
        {
            if (string.IsNullOrWhiteSpace(repoPath))
                throw new ArgumentException("Repo path cannot be empty.", nameof(repoPath));
            if (_repoPath?.Length > 0)
                return; // already set by ctor
            // NOTE: if you need to support changing repo between loads, add refresh logic here.
        }

        private void MergeHelperWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                RefreshConflicts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Merge Helper", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadConflicts()
        {
            try
            {
                var items = GitService.GetConflictTriplets(_repoPath);
                ConflictsList.ItemsSource = items;
                if (items.Count > 0) ConflictsList.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Conflicts", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshConflicts()
        {
            if (string.IsNullOrWhiteSpace(_repoPath))
            {
                MessageBox.Show(this, "Repository path not set.", "Merge Helper", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            _conflicts = GitService.GetConflictTriplets(_repoPath);
            ConflictsList.ItemsSource = _conflicts;
            ConflictsList.DisplayMemberPath = nameof(GitService.ConflictTriplet.Path);

            if (_conflicts.Count == 0)
            {
                Title = "Merge Conflicts (none)";
                ClearEditors();
                return;
            }

            // Select first conflict by default
            _index = 0;
            ConflictsList.SelectedIndex = _index;
            ShowConflict(_index);
        }

        private void ConflictsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var idx = ConflictsList.SelectedIndex;
            if (idx >= 0 && idx < _conflicts.Count)
            {
                _index = idx;
                ShowConflict(_index);
            }
        }

        private void ShowConflict(int idx)
        {
            if (idx < 0 || idx >= _conflicts.Count)
            {
                ClearEditors();
                return;
            }

            var c = _conflicts[idx];

            PathLabel.Text = c.Path ?? "(unknown)";
            BaseEditor.Text = c.BaseText ?? string.Empty;
            OursEditor.Text = c.OursText ?? string.Empty;
            TheirsEditor.Text = c.TheirsText ?? string.Empty;

            // Default merged content: prefer ours (you can choose other default)
            MergeEditor.Text = string.IsNullOrEmpty(OursEditor.Text)
                ? TheirsEditor.Text
                : OursEditor.Text;

            Title = $"Merge Conflicts  [{idx + 1}/{_conflicts.Count}] - {c.Path}";
        }

        private void ClearEditors()
        {
            PathLabel.Text = "";
            BaseEditor.Text = "";
            OursEditor.Text = "";
            TheirsEditor.Text = "";
            MergeEditor.Text = "";
        }

        // -------- Accept buttons --------

        private void AcceptBase_Click(object sender, RoutedEventArgs e)
        {
            if (_index < 0 || _index >= _conflicts.Count) return;
            MergeEditor.Text = BaseEditor.Text;
        }

        private void AcceptOurs_Click(object sender, RoutedEventArgs e)
        {
            if (_index < 0 || _index >= _conflicts.Count) return;
            MergeEditor.Text = OursEditor.Text;
        }

        private void AcceptTheirs_Click(object sender, RoutedEventArgs e)
        {
            if (_index < 0 || _index >= _conflicts.Count) return;
            MergeEditor.Text = TheirsEditor.Text;
        }

        // -------- Save/Resolve & navigation --------

        private void SaveResolve_Click(object sender, RoutedEventArgs e)
        {
            if (_index < 0 || _index >= _conflicts.Count) return;

            var c = _conflicts[_index];
            try
            {
                GitService.ResolveConflictWithText(_repoPath, c.Path, MergeEditor.Text);

                // Refresh the list after resolving one
                var lastPath = c.Path;
                RefreshConflicts();

                // Try to select the next unresolved conflict near the previous index
                if (_conflicts.Count > 0)
                {
                    var next = Math.Min(_index, _conflicts.Count - 1);
                    ConflictsList.SelectedIndex = next;
                    ShowConflict(next);
                }
                else
                {
                    MessageBox.Show(this, "All conflicts resolved!", "Merge Helper",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Resolve Conflict", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (_conflicts.Count == 0) return;
            var i = Math.Max(0, _index - 1);
            _index = i;
            ConflictsList.SelectedIndex = i;
            ShowConflict(i);
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_conflicts.Count == 0) return;
            var i = Math.Min(_conflicts.Count - 1, _index + 1);
            _index = i;
            ConflictsList.SelectedIndex = i;
            ShowConflict(i);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshConflicts();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}