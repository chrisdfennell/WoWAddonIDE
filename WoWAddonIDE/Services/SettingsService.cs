// File: WoWAddonIDE/Services/SettingsService.cs
using System;
using System.IO;
using Newtonsoft.Json;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Services
{
    /// <summary>
    /// Single source of truth for persisting IDESettings to
    /// %AppData%\WoWAddonIDE\settings.json.
    ///
    /// IMPORTANT: this uses Newtonsoft.Json because IDESettings relies on Newtonsoft
    /// attributes ([JsonIgnore] on GitHubToken, [JsonProperty] on the legacy token
    /// field) to keep the GitHub token OUT of settings.json (it lives in DPAPI secure
    /// storage). Serializing with System.Text.Json would ignore those attributes and
    /// leak the token in plaintext, so all persistence must go through here.
    /// </summary>
    public static class SettingsService
    {
        private static readonly string _dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WoWAddonIDE");

        private static readonly string _path = Path.Combine(_dir, "settings.json");

        public static string SettingsPath => _path;

        public static IDESettings Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    return JsonConvert.DeserializeObject<IDESettings>(json) ?? new IDESettings();
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("Failed to load settings, using defaults", ex);
            }

            return new IDESettings();
        }

        public static void Save(IDESettings settings)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_path, json);
            }
            catch (Exception ex)
            {
                LogService.Error("Failed to save settings", ex);
            }
        }
    }
}