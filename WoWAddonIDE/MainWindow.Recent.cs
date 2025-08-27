// File: MainWindow.Recent.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        // ---------- MRU config ----------
        private const int MRU_MAX = 10;
        private static string MruDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WoWAddonIDE");
        private static string MruPath => Path.Combine(MruDir, "recent-projects.json");

        // Local cache (in-memory)
        private List<string> _recentProjects = new();

        // Call once during startup (e.g., in MainWindow ctor after InitializeComponent)
        private void Recent_Init()
        {
            _recentProjects = Recent_Load();
            Recent_PruneStale();
            Recent_BuildMenu();
            Autosave_Init();
        }

        // Call whenever a project is successfully opened/created
        private void TouchRecentProject(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) return;

            rootPath = Path.GetFullPath(rootPath.Trim());
            _recentProjects.RemoveAll(p => string.Equals(p, rootPath, StringComparison.OrdinalIgnoreCase));
            _recentProjects.Insert(0, rootPath);

            if (_recentProjects.Count > MRU_MAX)
                _recentProjects = _recentProjects.Take(MRU_MAX).ToList();

            Recent_Save(_recentProjects);
            Recent_BuildMenu();
        }

        // ---------- Persistence ----------
        private static List<string> Recent_Load()
        {
            try
            {
                if (File.Exists(MruPath))
                {
                    var json = File.ReadAllText(MruPath);
                    return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                }
            }
            catch { /* ignore */ }
            return new List<string>();
        }

        private static void Recent_Save(List<string> items)
        {
            try
            {
                Directory.CreateDirectory(MruDir);
                var json = System.Text.Json.JsonSerializer.Serialize(items);
                File.WriteAllText(MruPath, json);
            }
            catch { /* ignore write errors */ }
        }

        private void Recent_PruneStale()
        {
            var before = _recentProjects.Count;
            _recentProjects = _recentProjects.Where(Directory.Exists).ToList();
            if (_recentProjects.Count != before)
                Recent_Save(_recentProjects);
        }

        // ---------- Menu UI ----------
        private void Recent_BuildMenu()
        {
            if (RecentProjectsMenu == null) return;

            RecentProjectsMenu.Items.Clear();

            if (_recentProjects.Count == 0)
            {
                var empty = new MenuItem
                {
                    Header = "(none)",
                    IsEnabled = false,
                    Icon = new TextBlock
                    {
                        Text = "❌",
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"),
                        FontSize = 14
                    }
                };
                RecentProjectsMenu.Items.Add(empty);
                return;
            }

            int index = 1;
            foreach (var path in _recentProjects)
            {
                var display = Recent_DisplayText(path, index);
                var mi = new MenuItem
                {
                    Header = display,
                    Tag = path,
                    Icon = new TextBlock
                    {
                        Text = "📂", // folder/project emoji
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"),
                        FontSize = 14
                    }
                };
                mi.Click += Recent_Open_Click;
                RecentProjectsMenu.Items.Add(mi);
                index++;
            }

            RecentProjectsMenu.Items.Add(new Separator());

            var clear = new MenuItem
            {
                Header = "Clear Recent",
                Icon = new TextBlock
                {
                    Text = "🧹",
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"),
                    FontSize = 14
                }
            };
            clear.Click += (s, e) =>
            {
                _recentProjects.Clear();
                Recent_Save(_recentProjects);
                Recent_BuildMenu();
            };
            RecentProjectsMenu.Items.Add(clear);
        }

        private static string Recent_DisplayText(string path, int number)
        {
            // Show: 1  AddonName — C:\...\Parent\AddonName
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var parent = Path.GetDirectoryName(path) ?? "";
            var compactParent = CompactPath(parent, 50);
            return $"_{number}  {name} — {compactParent}{Path.DirectorySeparatorChar}{name}";
        }

        private static string CompactPath(string fullPath, int maxChars)
        {
            if (string.IsNullOrEmpty(fullPath)) return fullPath;
            if (fullPath.Length <= maxChars) return fullPath;

            // Simple middle-ellipsis compaction: C:\Users\Foo\...\Bar\Baz
            var parts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Length <= 3) return fullPath; // nothing to compact nicely

            var first = parts.First();
            var last = parts.Last();
            var compact = $"{first}{Path.DirectorySeparatorChar}...{Path.DirectorySeparatorChar}{last}";
            return compact.Length <= maxChars ? compact : compact.Substring(0, Math.Min(compact.Length, maxChars));
        }

        // ---------- Handlers ----------
        private void RecentProjectsMenu_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            // Rebuild on open (cleans up stale entries if any)
            Recent_PruneStale();
            Recent_BuildMenu();
        }

        private void Recent_Open_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem mi) return;
            var path = mi.Tag as string;
            if (string.IsNullOrWhiteSpace(path)) return;

            if (!Directory.Exists(path))
            {
                MessageBox.Show(this, $"Folder not found:\n{path}", "Recent Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                Recent_PruneStale();
                Recent_BuildMenu();
                return;
            }

            // >>> Replace with your existing project open method if named differently <<<
            try
            {
                // If you have a central load method, call it here (e.g., LoadProject(path) or OpenProject(path))
                // Example (pseudo):
                // LoadProjectFromRoot(path);

                // Many projects have an EnsureProject(...) + SetProjectRoot + RefreshTree pattern:
                // SetProjectRoot(path); RefreshProjectTree(); etc.

                // For now, call the same logic you use after a successful "Open Project…" dialog:
                OpenProjectFromRecent(path);

                // Track in MRU again (move to top)
                TouchRecentProject(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open Recent Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Bridge: implement this to call your actual project-loading logic.
        /// Keeps MRU file isolated from Project code.
        /// </summary>
        private void OpenProjectFromRecent(string rootPath)
        {
            LoadProjectAtPath(rootPath);
            // after _project is set and tree refreshed
            FileWatch_Start(_project.RootPath);
        }

        private void OpenProject_Fallback_ShowPath(string rootPath)
        {
            // Temporary fallback so clicking MRU does something visible
            Status($"(MRU) Open project: {rootPath}");
        }
    }
}