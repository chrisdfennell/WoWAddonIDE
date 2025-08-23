using System;
using System.IO;

namespace WoWAddonIDE.Models
{
    public enum IdeTheme { Light, Dark }

    public class IDESettings
    {
        // Build / paths
        public string AddOnsPath { get; set; } = "";
        public string StagingPath { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WoWAddonIDE", "Staging");
        public bool AllowBuildInsideAddOns { get; set; } = false;
        public string PackageExcludes { get; set; } = ".psd, .tmp, .bak, .pdb";

        // Theme
        public IdeTheme Theme { get; set; } = IdeTheme.Dark;

        // Git / GitHub
        public string GitUserName { get; set; } = "";
        public string GitUserEmail { get; set; } = "";
        /// <summary>GitHub Personal Access Token (classic or fine-grained). Stored locally in settings.json.</summary>
        public string GitHubToken { get; set; } = "";
        /// <summary>Default remote URL (e.g., https://github.com/you/YourAddon.git)</summary>
        public string GitRemoteUrl { get; set; } = "";
        /// <summary>GitHub OAuth App Client ID (for OAuth sign-in)</summary>
        public string GitHubOAuthClientId { get; set; } = "";  // from your OAuth App

    }
}