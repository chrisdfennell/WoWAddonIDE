using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Windows
{
    public partial class ReleasePublisherWindow : Window
    {
        private readonly IDESettings _settings;

        // Values consumed by MainWindow:
        public string TagName => TagText.Text.Trim();
        public string ReleaseName => NameText.Text.Trim();
        public string AssetPath => AssetText.Text.Trim();
        public string Body => BodyText.Text;
        public bool Prerelease => PrereleaseCheck.IsChecked == true;

        public ReleasePublisherWindow(IDESettings settings)
        {
            InitializeComponent();
            _settings = settings;

            // Sensible defaults
            if (string.IsNullOrWhiteSpace(NameText.Text))
                NameText.Text = $"Release {DateTime.Now:yyyy-MM-dd}";

            // If user has a staging path, pre-select last produced .zip
            try
            {
                var staging = string.IsNullOrWhiteSpace(_settings.StagingPath)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WoWAddonIDE", "Staging")
                    : _settings.StagingPath;

                if (Directory.Exists(staging))
                {
                    var lastZip = new DirectoryInfo(staging).GetFiles("*.zip", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .FirstOrDefault();

                    if (lastZip != null)
                        AssetText.Text = lastZip.FullName;
                }
            }
            catch { /* best-effort only */ }
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Zip files (*.zip)|*.zip|All files (*.*)|*.*",
                Title = "Select release asset (.zip)"
            };
            if (dlg.ShowDialog(this) == true)
                AssetText.Text = dlg.FileName;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TagName))
            {
                MessageBox.Show(this, "Please enter a tag (e.g. v1.2.3).", "Publish", MessageBoxButton.OK, MessageBoxImage.Information);
                TagText.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(AssetPath) || !File.Exists(AssetPath))
            {
                MessageBox.Show(this, "Please select a valid .zip asset.", "Publish", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}