using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
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

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            // If we can't verify (no direct asset URL or no published checksum), fall back to
            // opening the release/download page in the browser rather than running something
            // unverified in-app.
            if (string.IsNullOrWhiteSpace(_result.DownloadUrl) ||
                string.IsNullOrWhiteSpace(_result.ExpectedSha256))
            {
                var fallback = _result.DownloadUrl ?? _result.ReleaseUrl;
                if (!string.IsNullOrWhiteSpace(fallback))
                    Process.Start(new ProcessStartInfo { FileName = fallback, UseShellExecute = true });
                Close();
                return;
            }

            // Verified download: fetch the .exe, check its SHA-256, and only keep it if it matches.
            IsEnabled = false;
            try
            {
                var fileName = _result.DownloadAssetName ?? $"WoWAddonIDE-{_result.LatestVersion}.exe";
                var dest = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", fileName);

                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                using (var resp = await http.GetAsync(_result.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    resp.EnsureSuccessStatusCode();
                    await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                    await resp.Content.CopyToAsync(fs);
                }

                string actual;
                using (var fs = File.OpenRead(dest))
                    actual = Convert.ToHexString(SHA256.HashData(fs)).ToLowerInvariant();

                if (!string.Equals(actual, _result.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(dest); } catch { /* best effort */ }
                    MessageBox.Show(this,
                        "The downloaded file failed SHA-256 verification and was deleted. " +
                        "Do not run it.\n\n" +
                        $"Expected: {_result.ExpectedSha256}\nActual:   {actual}",
                        "Update verification FAILED", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                MessageBox.Show(this,
                    $"Update downloaded and verified (SHA-256).\n\nSaved to:\n{dest}",
                    "Update ready", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reveal the verified file in Explorer so the user can run/replace it.
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{dest}\"",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex) { LogService.Warn("Failed to reveal downloaded update", ex); }

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Update download failed:\n{ex.Message}",
                    "Update", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled = true;
            }
        }

        private void ViewRelease_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_result.ReleaseUrl))
                Process.Start(new ProcessStartInfo { FileName = _result.ReleaseUrl, UseShellExecute = true });
        }
    }
}
