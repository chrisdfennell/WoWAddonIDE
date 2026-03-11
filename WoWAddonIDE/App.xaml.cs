// App.xaml.cs
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class App : Application
    {
        public App()
        {
            // Crash logging so silent failures get captured
            this.DispatcherUnhandledException += (s, e) =>
            {
                LogCrash("Dispatcher", e.Exception);
                MessageBox.Show(e.Exception.Message, "Unhandled UI Exception");
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex) LogCrash("AppDomain", ex);
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogCrash("TaskScheduler", e.Exception);
                e.SetObserved();
            };
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 1) Load settings (or create defaults)
            var settings = LoadSettings();

            // 2) Initialize theme manager
            ThemeManager.Initialize(settings);

            // 3) Wire up your persistence delegate (exactly as requested)
            ThemeManager.Persist = () =>
            {
                try
                {
                    var path = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "WoWAddonIDE", "settings.json");

                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var json = JsonSerializer.Serialize(
                        ThemeManager.Settings,
                        new JsonSerializerOptions { WriteIndented = true });

                    File.WriteAllText(path, json);
                }
                catch (Exception ex) { LogService.Warn("Failed to persist theme settings", ex); }
            };

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Best-effort save on exit too
            try { ThemeManager.Persist?.Invoke(); } catch (Exception ex) { LogService.Warn("Failed to save settings on exit", ex); }
            base.OnExit(e);
        }

        // ---------- helpers ----------

        private static IDESettings LoadSettings()
        {
            try
            {
                var path = GetSettingsPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    // Be tolerant of older files (comments/trailing commas)
                    var opts = new JsonSerializerOptions
                    {
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        PropertyNameCaseInsensitive = true
                    };
                    var loaded = JsonSerializer.Deserialize<IDESettings>(json, opts);
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception ex)
            {
                LogService.Warn("Failed to load settings at startup", ex);
            }

            return new IDESettings();
        }

        private static string GetSettingsPath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WoWAddonIDE");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }

        private static void LogCrash(string kind, Exception ex)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                       "WoWAddonIDE");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "crash.log");
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {ex}\n\n");
            }
            catch (Exception logEx) { LogService.Warn("Failed to write crash log", logEx); }
        }
    }
}