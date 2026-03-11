// Models/IDESettings.cs
using System.Collections.Generic;
using Newtonsoft.Json;

namespace WoWAddonIDE.Models
{
    /// <summary>Theme selection: follow OS (System) or force Light/Dark.</summary>
    public enum ThemeMode { System, Light, Dark }

    /// <summary>Persisted IDE settings (JSON-serialized to settings.json).</summary>
    public class IDESettings
    {
        // -------- UI / Theme --------
        public ThemeMode ThemeMode { get; set; } = ThemeMode.System;
        public double MainWindowWidth { get; set; } = 1200;
        public double MainWindowHeight { get; set; } = 800;
        public bool MainWindowMaximized { get; set; } = false;

        // -------- Paths --------
        /// <summary>WoW AddOns folder (e.g., ...\World of Warcraft\_retail_\Interface\AddOns)</summary>
        public string AddOnsPath { get; set; } = "";
        /// <summary>Staging/output folder used by "Build Zip..." etc.</summary>
        public string StagingPath { get; set; } = "";
        /// <summary>Last project root opened (optional convenience).</summary>
        public string LastProjectPath { get; set; } = "";
        /// <summary>Working folder where New/Open dialogs start.</summary>
        public string DefaultProjectRoot { get; set; } = "";

        // -------- Build / Packaging --------
        /// <summary>Space/comma separated extensions to exclude (e.g. ".psd .xcf .zip .7z .rar .git .vs bin obj").</summary>
        public string PackageExcludes { get; set; } =
            ".psd .xcf .zip .7z .rar .git .vs bin obj .pdb .user .cache .tmp";
        /// <summary>Safety valve: allow building when the project is already inside AddOns (not recommended).</summary>
        public bool AllowBuildInsideAddOns { get; set; } = false;
        public bool OpenAddOnsAfterBuild { get; set; } = false;

        // -------- Git / GitHub --------
        public string GitUserName { get; set; } = "";
        public string GitUserEmail { get; set; } = "";
        public string GitRemoteUrl { get; set; } = "";        // default "origin" URL

        /// <summary>
        /// GitHub token backed by DPAPI secure storage.
        /// The JSON property is kept for back-compat migration but cleared on load.
        /// New tokens are stored exclusively in the Windows credential vault.
        /// </summary>
        [JsonIgnore]
        public string GitHubToken
        {
            get => _gitHubToken ??= LoadSecureToken();
            set
            {
                _gitHubToken = value ?? "";
                SaveSecureToken(value ?? "");
            }
        }
        [JsonProperty("GitHubToken")]
        private string _gitHubTokenLegacy
        {
            get => ""; // never serialize the token to JSON
            set
            {
                // One-time migration: if an old settings file has a token, move it to vault
                if (!string.IsNullOrWhiteSpace(value))
                {
                    SaveSecureToken(value);
                    _gitHubToken = value;
                }
            }
        }
        private string? _gitHubToken;

        private static string LoadSecureToken()
        {
            try { return Services.SecureStorage.LoadString(Constants.SecureTokenKey) ?? ""; }
            catch { return ""; }
        }

        private static void SaveSecureToken(string value)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(value))
                    Services.SecureStorage.Delete(Constants.SecureTokenKey);
                else
                    Services.SecureStorage.SaveString(Constants.SecureTokenKey, value);
            }
            catch { /* vault unavailable -- fall back silently */ }
        }

        public string GitHubOAuthClientId { get; set; } = ""; // for device-code sign-in

        // Back-compat synonyms (not serialized)
        [JsonIgnore] public string GitName { get => GitUserName; set => GitUserName = value; }
        [JsonIgnore] public string GitEmail { get => GitUserEmail; set => GitUserEmail = value; }

        // -------- Old editor fields kept for back-compat --------
        public int TabSize { get; set; } = 4;
        public bool ConvertTabsToSpaces { get; set; } = true;
        public bool HighlightCurrentLine { get; set; } = true;

        // -------- New Editor UX (persisted) --------
        public string EditorFontFamily { get; set; } = "Consolas";
        public double EditorFontSize { get; set; } = 14;
        public bool EditorWordWrap { get; set; } = false;
        public bool EditorShowInvisibles { get; set; } = false;

        // API docs location (when user imports a custom JSON)
        public string ApiDocsPath { get; set; } = "";

        // -------- Aliases for new UI bindings (NOT serialized) --------
        [JsonIgnore] public int EditorTabSize { get => TabSize; set => TabSize = value; }
        [JsonIgnore] public bool UseSpacesInsteadOfTabs { get => ConvertTabsToSpaces; set => ConvertTabsToSpaces = value; }
        [JsonIgnore] public bool EditorHighlightCurrentLine { get => HighlightCurrentLine; set => HighlightCurrentLine = value; }

        // -------- Future-proof misc bag --------
        public Dictionary<string, string> Extras { get; set; } = new();

        // -------- Auto-save --------
        public bool AutoSaveEnabled { get; set; } = true;
        public int AutoSaveIntervalSecs { get; set; } = 30;
        public bool AutoSaveOnFocusLost { get; set; } = true;

        // -------- File watching / external changes --------
        public bool FileWatchEnabled { get; set; } = true;
        public bool AutoReloadIfUnmodified { get; set; } = true;

        // -------- Trailing Whitespace Removal on Save --------
        public bool TrimTrailingWhitespaceOnSave { get; set; } = true;
        public bool EnsureFinalNewlineOnSave { get; set; } = true;

        // -------- Lua Formatting (external tool) --------
        public string? LuaFormatterPath { get; set; } = null;     // path to your formatter exe (e.g., lua-fmt.exe)
        public string LuaFormatterArgs { get; set; } = "--stdin --stdout"; // args; supports placeholders (see below)
        public bool FormatOnSave { get; set; } = false;    // opt-in

    }
}