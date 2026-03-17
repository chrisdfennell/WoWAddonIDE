using System.Diagnostics;
using System.Windows;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class UpdateAvailableWindow : Window
    {
        private readonly UpdateCheckResult _result;

        public UpdateAvailableWindow(UpdateCheckResult result)
        {
            InitializeComponent();
            _result = result;

            CurrentVersionRun.Text = result.CurrentVersion;
            LatestVersionRun.Text = result.LatestVersion;
            ReleaseNotesBox.Text = string.IsNullOrWhiteSpace(result.ReleaseNotes)
                ? "(No release notes)"
                : result.ReleaseNotes;
        }

        private void Download_Click(object sender, RoutedEventArgs e)
        {
            var url = _result.DownloadUrl ?? _result.ReleaseUrl;
            if (!string.IsNullOrWhiteSpace(url))
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            Close();
        }

        private void ViewRelease_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_result.ReleaseUrl))
                Process.Start(new ProcessStartInfo { FileName = _result.ReleaseUrl, UseShellExecute = true });
        }
    }
}
