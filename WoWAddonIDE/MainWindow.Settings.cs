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
            catch (Exception ex) { LogService.Error("LoadSettings: failed to read settings file", ex); }
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
                Autosave_ApplySettings();
            }
        }

        /// <summary>
        /// Try to locate a Lua formatter (StyLua) and store path/args in _settings.
        /// Searches:
        /// - Existing configured path
        /// - PATH (stylua.exe)
        /// - AppBase\tools\stylua\stylua.exe   (legacy from earlier suggestion)
        /// - AppBase\Third_Party\Stylua\stylua.exe
        /// - Any parent of AppBase containing Third_Party\Stylua\stylua.exe (useful in dev)
        /// - ProjectRoot\Third_Party\Stylua\stylua.exe (when a project is open)
        /// - %ProgramFiles%\StyLua\stylua.exe
        /// - %LocalAppData%\Programs\StyLua\stylua.exe
        /// </summary>
        private void Formatter_Autodetect()
        {
            if (_settings == null) return;

            // If already configured and the exe exists, just normalize args and return.
            if (!string.IsNullOrWhiteSpace(_settings.LuaFormatterPath) &&
                File.Exists(_settings.LuaFormatterPath))
            {
                Formatter_MigrateArgsIfNeeded();
                return;
            }

            string appDir = AppContext.BaseDirectory;
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string localProg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

            // Also look in the project at: <root>\Third_Party\Stylua\stylua.exe
            string? thirdParty = (_project != null)
                ? Path.Combine(_project.RootPath, "Third_Party", "Stylua", "stylua.exe")
                : null;

            var candidates = new[]
            {
        _settings.LuaFormatterPath ?? "",                                     // whatever was saved previously
        "stylua.exe",                                                          // PATH lookup
        Path.Combine(appDir,    "tools", "stylua", "stylua.exe"),              // bundled with app (if you ship it)
        Path.Combine(progFiles, "StyLua", "stylua.exe"),                       // common manual install
        Path.Combine(localProg, "StyLua", "stylua.exe"),
        thirdParty ?? ""                                                       // project-local copy
    };

            foreach (var c in candidates)
            {
                var resolved = ResolveExecutable(c);
                if (resolved != null)
                {
                    _settings.LuaFormatterPath = resolved;

                    // Ensure sane args (fixes old --stdin/--stdout)
                    string args = _settings.LuaFormatterArgs ?? "";
                    bool legacy =
                        args.IndexOf("--stdin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        args.IndexOf("--stdout", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (string.IsNullOrWhiteSpace(args) || legacy)
                        _settings.LuaFormatterArgs = DEFAULT_STYLUA_ARGS; // e.g. "--search-parent-directories --stdin-filepath \"{file}\" -"

                    try { SettingsService.Save(_settings); } catch (Exception ex) { LogService.Warn("Formatter_Autodetect: failed to save settings", ex); }
                    Status($"Lua formatter detected: {resolved}");
                    return;
                }
            }

            // Not found — leave unset; the Format command will prompt the user.
        }

        /// <summary>
        /// If 'candidate' is a filename, search PATH. If it's a path, check existence.
        /// Returns full path or null.
        /// </summary>
        private static string? ResolveExecutable(string candidate)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(candidate)) return null;

                // Explicit path?
                if (candidate.Contains(Path.DirectorySeparatorChar) || candidate.Contains(Path.AltDirectorySeparatorChar))
                    return File.Exists(candidate) ? candidate : null;

                // Search PATH
                var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (var dir in path.Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)))
                {
                    var d = dir.Trim().Trim('"');
                    var p = Path.Combine(d, candidate);
                    if (File.Exists(p)) return p;
                    // Windows often has .exe implicit; try adding it
                    if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        p = Path.Combine(d, candidate + ".exe");
                        if (File.Exists(p)) return p;
                    }
                }
            }
            catch (Exception ex) { LogService.Warn("ResolveExecutable: failed to resolve candidate", ex); }
            return null;
        }

        /// <summary>
        /// Walks parent directories starting at 'startDir' and returns the first
        /// path that exists for the given relative parts (e.g., "Third_Party","Stylua","stylua.exe").
        /// </summary>
        private static string? FindUpwards(string startDir, params string[] parts)
        {
            try
            {
                var relative = Path.Combine(parts);
                var dir = new DirectoryInfo(startDir);
                while (dir != null)
                {
                    var probe = Path.Combine(dir.FullName, relative);
                    if (File.Exists(probe)) return probe;
                    dir = dir.Parent;
                }
            }
            catch (Exception ex) { LogService.Warn("FindUpwards: failed to search parent directories", ex); }
            return null;
        }
    }
}