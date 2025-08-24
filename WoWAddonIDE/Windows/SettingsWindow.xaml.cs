using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(IDESettings settings)
        {
            InitializeComponent();

            // Bind to the live settings object
            DataContext = ThemeManager.Settings;

            // Populate font list + common sizes / tabs
            FontFamilyBox.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(s => s);
            FontSizeBox.ItemsSource = new[] { 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24 };
            TabSizeBox.ItemsSource = new[] { 2, 3, 4, 8 };
        }

        private void BrowseAddOns_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new VistaFolderBrowserDialog { Description = "Select your WoW AddOns folder" };
            var current = ThemeManager.Settings.AddOnsPath;
            if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                dlg.SelectedPath = current;

            if (dlg.ShowDialog(this) == true)
            {
                ThemeManager.Settings.AddOnsPath = dlg.SelectedPath;
                AddOnsPathBox.Text = dlg.SelectedPath; // immediate UI update (no INotifyPropertyChanged)
            }
        }

        private void BrowseProjectRoot_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new VistaFolderBrowserDialog { Description = "Select a default parent folder for new projects" };
            var current = ThemeManager.Settings.DefaultProjectRoot;
            if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                dlg.SelectedPath = current;

            if (dlg.ShowDialog(this) == true)
            {
                ThemeManager.Settings.DefaultProjectRoot = dlg.SelectedPath;
                ProjectRootBox.Text = dlg.SelectedPath; // immediate UI update
            }
        }

        private void BrowseApiDocs_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select WoW API JSON"
            };
            if (ofd.ShowDialog(this) == true)
            {
                ThemeManager.Settings.ApiDocsPath = ofd.FileName;
                ApiDocsPathBox.Text = ofd.FileName;
            }
        }

        private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
        {
            var def = new IDESettings();
            ThemeManager.UpdateFrom(def);

            // Nudge visible controls so the user sees changes immediately
            AddOnsPathBox.Text = ThemeManager.Settings.AddOnsPath;
            ProjectRootBox.Text = ThemeManager.Settings.DefaultProjectRoot;
            ApiDocsPathBox.Text = ThemeManager.Settings.ApiDocsPath;
            FontFamilyBox.Text = ThemeManager.Settings.EditorFontFamily;
            FontSizeBox.Text = ThemeManager.Settings.EditorFontSize.ToString();
            TabSizeBox.Text = ThemeManager.Settings.EditorTabSize.ToString();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // Persist to %APPDATA%\WoWAddonIDE\settings.json
            ThemeManager.Persist?.Invoke();
            MessageBox.Show(this, "Settings saved.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Persist?.Invoke();
            DialogResult = true;
            Close();
        }
    }
}