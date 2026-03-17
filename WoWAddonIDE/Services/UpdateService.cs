using System;
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

                // Find .exe asset
                foreach (var asset in release.Assets)
                {
                    if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        result.DownloadUrl = asset.BrowserDownloadUrl;
                        break;
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
    }
}
