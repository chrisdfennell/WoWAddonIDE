using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WoWAddonIDE.Models;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private void Toolbar_ToggleWrap_Click(object sender, RoutedEventArgs e)
        {
            _wordWrap = !_wordWrap;
            foreach (var ed in AllEditors())
                ed.WordWrap = _wordWrap;
            Status("Word wrap: " + (_wordWrap ? "ON" : "OFF"));
        }

        private void Toolbar_ToggleInvis_Click(object sender, RoutedEventArgs e)
        {
            _showInvisibles = !_showInvisibles;
            foreach (var ed in AllEditors())
            {
                ed.Options.ShowSpaces = _showInvisibles;
                ed.Options.ShowTabs = _showInvisibles;
                ed.Options.ShowEndOfLine = _showInvisibles;
            }
            Status("Invisibles: " + (_showInvisibles ? "ON" : "OFF"));
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            new Windows.AboutWindow { Owner = this }.ShowDialog();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            OpenHelpWindow();
        }

        private void Toolbar_GoToDef_Click(object sender, RoutedEventArgs e)
        {
            var ed = ActiveEditor();
            if (ed == null) { Status("No editor"); return; }
            GoToDefinition(ed);
        }

        private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshOutlineForActive();
        }

        private void OpenHelpWindow()
        {
            new Windows.HelpWindow { Owner = this }.ShowDialog();
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void New_Click(object s, RoutedEventArgs e) => NewProject_Click(s, e);
        private void Open_Click(object s, RoutedEventArgs e) => OpenProject_Click(s, e);
        private void Save_Click(object sender, RoutedEventArgs e) => SaveActiveTab();

        // Changed from private to internal to be accessible across partial class files.
        internal void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (TabItem tab in EditorTabs.Items) SaveTab(tab);
            Status("Saved all files");
        }

        private void OpenAddOnsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !System.IO.Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set or invalid. Go to Tools > Settings.", "Open AddOns Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Process.Start("explorer.exe", _settings.AddOnsPath);
        }

        private void OpenStagingFolder_Click(object sender, RoutedEventArgs e)
        {
            var staging = string.IsNullOrWhiteSpace(_settings.StagingPath)
                ? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "WoWAddonIDE", "Staging")
                : _settings.StagingPath;

            try { System.IO.Directory.CreateDirectory(staging); } catch { /* ignore */ }
            Process.Start("explorer.exe", staging);
        }

        private void Clean_Click(object sender, RoutedEventArgs e) => Output.Clear();

        private void ShowDiff_Click(object sender, RoutedEventArgs e)
        {
            if (EditorTabs.SelectedItem is not TabItem tab || tab.Content is not ICSharpCode.AvalonEdit.TextEditor ed) return;
            if (tab.Tag is not string path || !System.IO.File.Exists(path)) return;

            var disk = System.IO.File.ReadAllText(path);
            var buf = ed.Text;

            var dw = new Windows.DiffWindow { Owner = this };
            dw.ShowDiff(disk, buf);
            dw.ShowDialog();
        }
    }
}