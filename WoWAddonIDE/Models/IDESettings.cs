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
        /// <summary>Staging/output folder used by “Build Zip…” etc.</summary>
        public string StagingPath { get; set; } = "";
        /// <summary>Last project root opened (optional convenience).</summary>
        public string LastProjectPath { get; set; } = "";

        // -------- Build / Packaging --------
        /// <summary>Space/comma separated extensions to exclude (e.g. ".psd .xcf .zip .7z .rar .git .vs bin obj").</summary>
        public string PackageExcludes { get; set; } =
            ".psd .xcf .zip .7z .rar .git .vs bin obj .pdb .user .cache .tmp";
        /// <summary>Safety valve: allow building when the project is already inside AddOns (not recommended).</summary>
        public bool AllowBuildInsideAddOns { get; set; } = false;

        // -------- Git / GitHub --------
        // Canonical names used by GitService & GitAuthWindow:
        public string GitUserName { get; set; } = "";
        public string GitUserEmail { get; set; } = "";
        public string GitRemoteUrl { get; set; } = "";        // default “origin” URL
        public string GitHubToken { get; set; } = "";         // PAT or OAuth token
        public string GitHubOAuthClientId { get; set; } = ""; // for device-code sign-in

        // Back-compat synonyms (not serialized) in case some code still reads GitName/GitEmail:
        [JsonIgnore] public string GitName { get => GitUserName; set => GitUserName = value; }
        [JsonIgnore] public string GitEmail { get => GitUserEmail; set => GitUserEmail = value; }

        // -------- Editor defaults --------
        public int TabSize { get; set; } = 4;
        public bool ConvertTabsToSpaces { get; set; } = true;
        public bool HighlightCurrentLine { get; set; } = true;

        // -------- Future-proof misc bag --------
        public Dictionary<string, string> Extras { get; set; } = new();
    }
}