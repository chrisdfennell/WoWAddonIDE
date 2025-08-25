using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;
using WoWAddonIDE.Windows;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private IDESettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    return JsonConvert.DeserializeObject<IDESettings>(json) ?? new IDESettings();
                }
            }
            catch { /* ignore */ }
            return new IDESettings();
        }

        // Changed from private to internal to be accessible across partial class files.
        internal void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_settingsPath,
                    JsonConvert.SerializeObject(_settings, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log($"Failed to save settings: {ex.Message}");
            }
        }

        private static string DetectAddOnsPath()
        {
            var candidates = new[]
            {
                @"C:\Program Files (x86)\World of Warcraft\_retail_\Interface\AddOns",
                @"C:\Program Files\World of Warcraft\_retail_\Interface\AddOns",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"World of Warcraft","_retail_","Interface","AddOns"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"World of Warcraft","_retail_","Interface","AddOns")
            };
            return candidates.FirstOrDefault(Directory.Exists) ?? "";
        }

        public void Settings_Click(object? sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(ThemeManager.Settings) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                ReapplyEditorThemeToOpenTabs();
            }
        }
    }
}