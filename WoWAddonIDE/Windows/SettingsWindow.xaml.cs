using System;
using System.IO;
using System.Windows;
using Ookii.Dialogs.Wpf;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Windows
{
    public partial class SettingsWindow : Window
    {
        public IDESettings Settings { get; private set; }

        public SettingsWindow(IDESettings settings)
        {
            InitializeComponent();
            Settings = settings;
            AddOns.Text = Settings.AddOnsPath;
            Staging.Text = Settings.StagingPath;
            AllowInside.IsChecked = Settings.AllowBuildInsideAddOns;
            Excludes.Text = Settings.PackageExcludes;
        }

        private void BrowseAddOns_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new VistaFolderBrowserDialog { Description = "Select World of Warcraft _retail_\\Interface\\AddOns" };
            if (dlg.ShowDialog(this) == true) AddOns.Text = dlg.SelectedPath;
        }

        private void BrowseStaging_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new VistaFolderBrowserDialog { Description = "Select a staging/output folder" };
            if (dlg.ShowDialog(this) == true) Staging.Text = dlg.SelectedPath;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Settings.AddOnsPath = AddOns.Text.Trim();
            Settings.StagingPath = Staging.Text.Trim();
            Settings.AllowBuildInsideAddOns = AllowInside.IsChecked == true;
            Settings.PackageExcludes = Excludes.Text.Trim();

            // Ensure staging exists
            try { if (!string.IsNullOrWhiteSpace(Settings.StagingPath)) Directory.CreateDirectory(Settings.StagingPath); }
            catch { /* ignore */ }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}