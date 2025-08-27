// File: MainWindow.FileWatch.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private FileSystemWatcher? _fsw;
        private readonly Dictionary<string, DateTime> _recentSaves = new(StringComparer.OrdinalIgnoreCase);

        // Settings (safe defaults if your settings class doesn’t have them yet)
        private bool FileWatchEnabled => _settings?.FileWatchEnabled ?? true;
        private bool AutoReloadIfUnmodified => _settings?.AutoReloadIfUnmodified ?? true;
        private static readonly string[] _watchExts = { ".lua", ".xml", ".toc", ".txt", ".md" };

        // Call whenever a project is (re)loaded
        private void FileWatch_Start(string projectRoot)
        {
            FileWatch_Stop();

            if (!FileWatchEnabled) return;
            if (string.IsNullOrWhiteSpace(projectRoot) || !Directory.Exists(projectRoot)) return;

            _fsw = new FileSystemWatcher(projectRoot)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Size
                             | NotifyFilters.CreationTime
            };

            _fsw.Changed += (s, e) => OnFsEventSafe(() => OnDiskChanged(e.FullPath));
            _fsw.Deleted += (s, e) => OnFsEventSafe(() => OnDiskDeleted(e.FullPath));
            _fsw.Renamed += (s, e) => OnFsEventSafe(() => OnDiskRenamed(e.OldFullPath, e.FullPath));
            // Created is usually covered by Changed for our needs; add if you want
        }

        private void FileWatch_Stop()
        {
            if (_fsw == null) return;
            try
            {
                _fsw.EnableRaisingEvents = false;
                _fsw.Dispose();
            }
            catch { /* ignore */ }
            finally { _fsw = null; }
        }

        // Note: call this right after writing a file to disk so we can ignore our own change events
        private void FileWatch_NoteJustSaved(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            _recentSaves[path] = DateTime.UtcNow;

            // opportunistic cleanup
            var cutoff = DateTime.UtcNow - TimeSpan.FromSeconds(5);
            var oldKeys = _recentSaves.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
            foreach (var k in oldKeys) _recentSaves.Remove(k);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Event handling
        // ─────────────────────────────────────────────────────────────────────────────
        private void OnFsEventSafe(Action action)
        {
            // marshal back to UI thread
            try
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Background, action);
            }
            catch { /* ignore */ }
        }

        private void OnDiskChanged(string path)
        {
            if (!ShouldWatchThis(path)) return;

            // Suppress our own save events
            if (_recentSaves.TryGetValue(path, out var when) &&
                (DateTime.UtcNow - when) < TimeSpan.FromSeconds(1.0))
                return;

            var tab = FindOpenTab(path);
            if (tab == null) return; // not open → ignore

            bool isDirty = IsTabDirty(tab);

            if (!isDirty && AutoReloadIfUnmodified)
            {
                ReloadTabFromDisk(tab, path, silent: true);
                return;
            }

            // Prompt
            var res = MessageBox.Show(this,
                $"File was modified on disk:\n{path}\n\nReload it in the editor?",
                "File Changed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                ReloadTabFromDisk(tab, path, silent: false);
            }
            // No → keep editor contents (you are now "out-of-date" vs disk)
        }

        private void OnDiskDeleted(string path)
        {
            if (!ShouldWatchThis(path)) return;
            var tab = FindOpenTab(path);
            if (tab == null) return;

            var res = MessageBox.Show(this,
                $"File was deleted on disk:\n{path}\n\nClose this tab?",
                "File Deleted",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res == MessageBoxResult.Yes)
                EditorTabs.Items.Remove(tab);
        }

        private void OnDiskRenamed(string oldPath, string newPath)
        {
            // Only care if we had the old path open
            var tab = FindOpenTab(oldPath);
            if (tab == null) return;

            // If the file still exists at the new location and extension is watched, rebind tab
            bool newExists = File.Exists(newPath);
            if (newExists && ShouldWatchThis(newPath))
            {
                // Update tag + header (preserve dirty star if any)
                var wasDirty = IsTabDirty(tab);
                tab.Tag = newPath;
                var name = System.IO.Path.GetFileName(newPath);
                tab.Header = wasDirty ? name + "*" : name;

                Status($"File renamed: {name}");
            }
            else
            {
                // Otherwise treat like a delete for our purposes
                OnDiskDeleted(oldPath);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────────
        private bool ShouldWatchThis(string path)
        {
            try
            {
                var ext = System.IO.Path.GetExtension(path);
                return _watchExts.Contains(ext, StringComparer.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private TabItem? FindOpenTab(string path)
        {
            foreach (var obj in EditorTabs.Items)
            {
                if (obj is TabItem tab && tab.Tag is string p &&
                    string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
                    return tab;
            }
            return null;
        }

        private static bool IsTabDirty(TabItem tab)
        {
            var header = tab.Header as string ?? tab.Header?.ToString() ?? "";
            return header.EndsWith('*'); // char overload (perf)
        }

        private void ReloadTabFromDisk(TabItem tab, string path, bool silent)
        {
            try
            {
                if (!File.Exists(path)) return;
                if (tab.Content is not TextEditor ed) return;

                // Preserve caret + scroll (read-only ScrollOffset -> use ScrollTo* to restore)
                int caret = ed.CaretOffset;
                var scroll = ed.TextArea.TextView.ScrollOffset; // Vector (read-only)

                // Reload text
                ed.Text = File.ReadAllText(path);
                MarkTabDirty(path, false);
                UpdateEditorStatus(ed);

                // Restore caret
                ed.CaretOffset = Math.Min(caret, ed.Document.TextLength);

                // Restore scroll position
                ed.ScrollToHorizontalOffset(scroll.X);
                ed.ScrollToVerticalOffset(scroll.Y);

                if (!silent)
                    Status($"Reloaded: {System.IO.Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Reload Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}