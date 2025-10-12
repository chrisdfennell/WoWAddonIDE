// File: WoWAddonIDE/Services/SettingsService.cs
using System;
using System.IO;
using System.Text.Json;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Services
{
    /// <summary>
    /// Tiny helper to persist IDESettings to %AppData%\WoWAddonIDE\settings.json
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
                    var opts = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    return JsonSerializer.Deserialize<IDESettings>(json, opts) ?? new IDESettings();
                }
            }
            catch
            {
                // ignore and return defaults
            }

            return new IDESettings();
        }

        public static void Save(IDESettings settings)
        {
            try
            {
                Directory.CreateDirectory(_dir);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(settings, opts);
                File.WriteAllText(_path, json);
            }
            catch
            {
                // swallow IO issues; you can log to your Output box if desired
            }
        }
    }
}