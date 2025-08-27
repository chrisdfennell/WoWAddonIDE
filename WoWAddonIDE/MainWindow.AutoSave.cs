// File: MainWindow.AutoSave.cs
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        // Timer for periodic autosave
        private DispatcherTimer? _autoSaveTimer;

        // Defaults if your _settings doesn’t have these yet
        private bool AutoSaveEnabled => _settings?.AutoSaveEnabled ?? true;   // default ON
        private int AutoSaveIntervalSecs => _settings?.AutoSaveIntervalSecs ?? 30;     // default 30s
        private bool AutoSaveOnFocusLost => _settings?.AutoSaveOnFocusLost ?? true;   // default ON

        /// <summary>
        /// Call ONCE after InitializeComponent() and after _settings is loaded.
        /// </summary>
        private void Autosave_Init()
        {
            // Create timer once
            _autoSaveTimer ??= new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(Math.Max(5, AutoSaveIntervalSecs))
            };
            _autoSaveTimer.Tick -= AutoSaveTimer_Tick;
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;

            // Window/app focus loss = quick autosave (optional)
            this.Deactivated -= Window_Deactivated_AutoSave;
            this.Deactivated += Window_Deactivated_AutoSave;

            // Safety net when closing
            this.Closing -= MainWindow_Closing_AutoSave;
            this.Closing += MainWindow_Closing_AutoSave;

            Autosave_ApplySettings();
        }

        /// <summary>
        /// Re-read settings and apply (call after Settings dialog OK).
        /// </summary>
        private void Autosave_ApplySettings()
        {
            if (_autoSaveTimer == null) return;

            _autoSaveTimer.Interval = TimeSpan.FromSeconds(Math.Max(5, AutoSaveIntervalSecs));

            if (AutoSaveEnabled)
            {
                if (!_autoSaveTimer.IsEnabled) _autoSaveTimer.Start();
            }
            else
            {
                if (_autoSaveTimer.IsEnabled) _autoSaveTimer.Stop();
            }
        }

        private void AutoSaveTimer_Tick(object? sender, EventArgs e)
        {
            if (!AutoSaveEnabled) return;
            SaveDirtyOpenFiles(source: "autosave");
        }

        private void Window_Deactivated_AutoSave(object? sender, EventArgs e)
        {
            if (!AutoSaveEnabled || !AutoSaveOnFocusLost) return;
            SaveDirtyOpenFiles(source: "focus");
        }

        private void MainWindow_Closing_AutoSave(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Last-chance save (always do it if autosave is on)
            if (!AutoSaveEnabled) return;
            SaveDirtyOpenFiles(source: "closing");
        }

        /// <summary>
        /// Saves any open tabs that look dirty (header ends with '*').
        /// </summary>
        private void SaveDirtyOpenFiles(string source)
        {
            int saved = 0;

            foreach (var obj in EditorTabs.Items)
            {
                if (obj is not TabItem tab) continue;
                var header = tab.Header as string ?? tab.Header?.ToString() ?? "";
                if (!header.EndsWith("*")) continue; // not marked dirty by MarkTabDirty

                try
                {
                    SaveTab(tab);
                    saved++;
                }
                catch
                {
                    // Swallow per-file errors so one failure doesn’t stop the rest
                }
            }

            if (saved > 0)
            {
                Status($"Auto-saved {saved} file{(saved == 1 ? "" : "s")} ({source}).");
                Log($"Auto-save ({source}): {saved} file(s) saved.");
            }
        }
    }
}
