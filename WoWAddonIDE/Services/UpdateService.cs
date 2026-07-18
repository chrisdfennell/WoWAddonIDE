using System;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Octokit;

namespace WoWAddonIDE.Services
{
    public class UpdateCheckResult
    {
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string ReleaseUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string? DownloadUrl { get; set; }
        public string? DownloadAssetName { get; set; }
        /// <summary>Expected lowercase SHA-256 of the .exe asset, from a published checksum asset (if any).</summary>
        public string? ExpectedSha256 { get; set; }
        public string? Error { get; set; }
    }

    public static class UpdateService
    {
        /// <summary>
        /// Checks GitHub for the latest release and compares to the running version.
        /// </summary>
        public static async Task<UpdateCheckResult> CheckForUpdateAsync()
        {
            var result = new UpdateCheckResult();

            try
            {
                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version
                    ?? new Version(0, 0, 0);
                result.CurrentVersion = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}";

                var client = new GitHubClient(new ProductHeaderValue(Constants.AppName, result.CurrentVersion));

                var release = await client.Repository.Release.GetLatest(
                    Constants.GitHubRepoOwner,
                    Constants.GitHubRepoName);

                if (release == null)
                {
                    result.Error = "No releases found.";
                    return result;
                }

                // Parse tag (e.g. "v1.2.0" -> "1.2.0")
                var tagName = release.TagName.TrimStart('v', 'V');
                result.LatestVersion = tagName;
                result.ReleaseUrl = release.HtmlUrl;
                result.ReleaseNotes = release.Body ?? "";

                // Find the .exe asset and any checksum asset.
                ReleaseAsset? exeAsset = null;
                ReleaseAsset? checksumAsset = null;
                foreach (var asset in release.Assets)
                {
                    if (exeAsset == null && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        exeAsset = asset;
                    if (asset.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) ||
                        asset.Name.Equals("SHA256SUMS", StringComparison.OrdinalIgnoreCase))
                        checksumAsset = asset;
                }

                if (exeAsset != null)
                {
                    result.DownloadUrl = exeAsset.BrowserDownloadUrl;
                    result.DownloadAssetName = exeAsset.Name;
                }

                // Fetch the expected SHA-256 so the download can be verified before use.
                if (exeAsset != null && checksumAsset != null)
                {
                    try
                    {
                        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                        var text = await http.GetStringAsync(checksumAsset.BrowserDownloadUrl);
                        result.ExpectedSha256 = ParseSha256(text, exeAsset.Name);
                    }
                    catch (Exception ex)
                    {
                        LogService.Warn("Failed to fetch update checksum", ex);
                    }
                }

                // Compare versions
                if (Version.TryParse(tagName, out var latestParsed))
                {
                    var currentComparable = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build);
                    result.UpdateAvailable = latestParsed > currentComparable;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                LogService.Warn("Update check failed", ex);
            }

            return result;
        }

        /// <summary>
        /// Extracts the SHA-256 for <paramref name="exeName"/> from checksum-file content.
        /// Handles a bare hash, "&lt;hash&gt;  file", "&lt;hash&gt; *file", and multi-line SHA256SUMS.
        /// </summary>
        internal static string? ParseSha256(string content, string exeName)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            string? firstHexOnly = null;
            foreach (var raw in content.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                var token = parts[0].Trim();
                if (!IsHex64(token)) continue;

                // Bare "<hash>" with no filename column — remember as a fallback.
                if (parts.Length == 1)
                {
                    firstHexOnly ??= token;
                    continue;
                }

                // "<hash>  filename" or "<hash> *filename" — require the filename to match.
                var fname = parts[^1].TrimStart('*');
                if (fname.EndsWith(exeName, StringComparison.OrdinalIgnoreCase))
                    return token.ToLowerInvariant();
            }

            return firstHexOnly?.ToLowerInvariant();
        }

        private static bool IsHex64(string s)
        {
            if (s.Length != 64) return false;
            foreach (var c in s)
                if (!Uri.IsHexDigit(c)) return false;
            return true;
        }
    }
}
