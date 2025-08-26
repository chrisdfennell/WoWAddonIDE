// File: WoWAddonIDE/MainWindow.Project.cs
using Microsoft.VisualBasic;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        // Dedup guard for the project tree
        private readonly HashSet<string> _treeSeen = new(StringComparer.OrdinalIgnoreCase);

        // Folders we never show in the Project Explorer
        private static readonly HashSet<string> _hiddenFolders =
            new(StringComparer.OrdinalIgnoreCase) { ".git", ".vs" };

        // ─────────────────────────────────────────────────────────────────────────────
        // Project: create / open
        // ─────────────────────────────────────────────────────────────────────────────
        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            var folderDlg = new VistaFolderBrowserDialog
            {
                Description = "Choose a workspace folder to create your addon project inside."
            };
            if (folderDlg.ShowDialog(this) != true) return;

            var addonName = Interaction.InputBox("Addon name:", "New Addon", "MyAddon");
            if (string.IsNullOrWhiteSpace(addonName)) return;

            try
            {
                var projRoot = Path.Combine(folderDlg.SelectedPath, addonName);
                Directory.CreateDirectory(projRoot);

                var toc = Path.Combine(projRoot, $"{addonName}.toc");
                File.WriteAllText(toc, TocParser.GenerateDefaultToc(addonName, "110005"));

                var mainLua = Path.Combine(projRoot, "Main.lua");
                File.WriteAllText(mainLua,
@"-- Main entry
local ADDON_NAME, ns = ...
print(ADDON_NAME .. ' loaded!')
");

                _project = AddonProject.LoadFromDirectory(projRoot);
                RefreshProjectTree();
                OpenFileInTab(mainLua);

                Status($"Created project: {addonName}");
                PathText.Text = $"Project: {_project.RootPath}";
                Log($"New project created at {projRoot}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "New Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            var folderDlg = new VistaFolderBrowserDialog
            {
                Description = "Select your addon project folder (where the .toc is)."
            };
            if (folderDlg.ShowDialog(this) != true) return;

            try
            {
                _project = AddonProject.LoadFromDirectory(folderDlg.SelectedPath);
                if (_project == null)
                {
                    MessageBox.Show(this, "No .toc found in that directory.", "Open Project",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                RefreshProjectTree();
                Status($"Opened project: {_project.Name}");
                PathText.Text = $"Project: {_project.RootPath}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open Project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Add files
        // ─────────────────────────────────────────────────────────────────────────────
        private void AddLua_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;

            var name = Interaction.InputBox("New Lua filename (without path):", "Add Lua File", "NewFile.lua");
            if (string.IsNullOrWhiteSpace(name)) return;

            var full = Path.Combine(_project!.RootPath, name);
            if (!full.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) full += ".lua";

            File.WriteAllText(full, "-- New Lua file\n");

            _project = AddonProject.LoadFromDirectory(_project.RootPath);
            RefreshProjectTree();
            OpenFileInTab(full);
            TryAddToToc(full);
        }

        private void AddXml_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;

            var name = Interaction.InputBox("New XML filename (without path):", "Add XML File", "Frame");
            if (string.IsNullOrWhiteSpace(name)) return;

            var full = Path.Combine(_project!.RootPath, name);
            if (!full.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) full += ".xml";

            File.WriteAllText(full,
@"<Ui xmlns=""http://www.blizzard.com/wow/ui/"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
    <!-- Add your frames here -->
</Ui>");

            _project = AddonProject.LoadFromDirectory(_project.RootPath);
            RefreshProjectTree();
            OpenFileInTab(full);
            TryAddToToc(full);
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Build / package
        // ─────────────────────────────────────────────────────────────────────────────
        private void Build_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            SaveAll_Click(sender, e);

            foreach (var p in LuaLint.Pass(_project!)) Log(p);

            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set or invalid. Use Tools → Settings.", "Build",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var src = Path.GetFullPath(_project!.RootPath);
                var addons = Path.GetFullPath(_settings.AddOnsPath);
                var target = Path.GetFullPath(Path.Combine(addons, _project!.Name));

                var sourceInsideAddons = src.StartsWith(addons, StringComparison.OrdinalIgnoreCase);
                if (sourceInsideAddons && !_settings.AllowBuildInsideAddOns)
                {
                    MessageBox.Show(this,
                        "Your project folder is already inside AddOns.\n" +
                        "To avoid data loss, Build is disabled.\n\n" +
                        "You can enable it in Tools → Settings (not recommended).",
                        "Build Skipped", MessageBoxButton.OK, MessageBoxImage.Information);
                    Log("Build skipped: project resides inside AddOns.");
                    return;
                }

                if (string.Equals(src, target, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "Source and target are the same folder. Aborting.",
                        "Build", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.CreateDirectory(target);

                CopyDirectorySafe(src, target);
                Status("Build succeeded (copied to AddOns).");
                Log($"Build copied to: {target}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Build", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildToFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            SaveAll_Click(sender, e);

            var dlg = new VistaFolderBrowserDialog { Description = "Choose a folder to build the addon into." };
            if (dlg.ShowDialog(this) != true) return;

            var src = Path.GetFullPath(_project!.RootPath);
            var target = Path.Combine(dlg.SelectedPath, _project!.Name);

            if (string.Equals(src, Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Source and target are the same folder. Pick a different destination.",
                    "Build to Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);

            CopyDirectorySafe(src, target);

            Status("Build to folder completed.");
            Log($"Build copied to: {target}");
        }

        private void BuildZip_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            SaveAll_Click(sender, e);

            try
            {
                var src = Path.GetFullPath(_project!.RootPath);
                var staging = string.IsNullOrWhiteSpace(_settings.StagingPath)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WoWAddonIDE", "Staging")
                    : _settings.StagingPath;

                Directory.CreateDirectory(staging);

                var tempFolder = Path.Combine(staging, "_build_temp_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempFolder);

                var stageTarget = Path.Combine(tempFolder, _project!.Name);
                CopyDirectorySafe(src, stageTarget, _settings.PackageExcludes);

                var zipPath = Path.Combine(staging, $"{_project!.Name}-{DateTime.Now:yyyyMMdd-HHmm}.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempFolder, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

                try { Directory.Delete(tempFolder, true); } catch { /* ignore */ }

                Status("Build Zip completed.");
                Log($"Package written: {zipPath}");
                Process.Start("explorer.exe", $"/select,\"{zipPath}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Build Zip", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Project tree (UI)
        // ─────────────────────────────────────────────────────────────────────────────
        private void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // optional: selection-dependent UI
        }

        private void ProjectTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProjectTree.SelectedItem is TreeViewItem tvi && tvi.Tag is string path && File.Exists(path))
            {
                OpenFileInTab(path);
            }
        }

        private string? SelectedTreePath()
        {
            if (ProjectTree.SelectedItem is TreeViewItem tvi && tvi.Tag is string p) return p;
            return null;
        }

        private void ProjectTree_Open_Click(object s, RoutedEventArgs e)
        {
            var p = SelectedTreePath(); if (p == null) return;
            if (File.Exists(p)) OpenFileInTab(p);
            else if (Directory.Exists(p)) Process.Start("explorer.exe", p);
        }

        private void ProjectTree_Reveal_Click(object s, RoutedEventArgs e)
        {
            var p = SelectedTreePath(); if (p == null) return;
            if (File.Exists(p)) Process.Start("explorer.exe", $"/select,\"{p}\"");
            else if (Directory.Exists(p)) Process.Start("explorer.exe", p);
        }

        private void ProjectTree_CopyPath_Click(object s, RoutedEventArgs e)
        {
            var p = SelectedTreePath(); if (p == null) return;
            try { Clipboard.SetText(p); } catch { /* ignore clipboard exceptions */ }
        }

        private void ProjectTree_CopyRelPath_Click(object s, RoutedEventArgs e)
        {
            var p = SelectedTreePath(); if (p == null) return;
            try
            {
                var rel = (_project != null) ? Path.GetRelativePath(_project.RootPath, p) : p;
                Clipboard.SetText(rel);
            }
            catch { /* ignore */ }
        }

        // Build the tree for the current _project
        private void RefreshProjectTree()
        {
            ProjectTree.Items.Clear();
            _treeSeen.Clear();

            if (_project == null || string.IsNullOrWhiteSpace(_project.RootPath))
                return;

            var rootPath = _project.RootPath;
            var rootNode = new TreeViewItem
            {
                Header = Path.GetFileName(rootPath),
                Tag = rootPath,
                IsExpanded = true
            };

            ProjectTree.Items.Add(rootNode);
            AddDirectory(rootNode, rootPath);
        }



        private static bool ShouldHideDir(string pathOrName)
        {
            var name = System.IO.Path.GetFileName(pathOrName.TrimEnd('\\', '/'));
            if (_hiddenFolders.Contains(name)) return true;

            // Also hide Windows "Hidden" or "System" directories
            try
            {
                var di = new System.IO.DirectoryInfo(pathOrName);
                var attrs = di.Attributes;
                if ((attrs & System.IO.FileAttributes.Hidden) != 0) return true;
                if ((attrs & System.IO.FileAttributes.System) != 0) return true;
            }
            catch { /* ignore */ }

            return false;
        }


        // Adds the folder content under 'parent'
        private void AddDirectory(TreeViewItem parent, string dir)
        {
            // 1) Add the .toc (once)
            var tocPath = Directory.EnumerateFiles(dir, "*.toc", SearchOption.TopDirectoryOnly)
                                   .FirstOrDefault();
            if (!string.IsNullOrEmpty(tocPath))
                TryAddFileNode(parent, tocPath);

            // 2) Add subfolders (skip hidden + .git/.vs)
            foreach (var sub in Directory.EnumerateDirectories(dir)
                                         .Where(d => !ShouldHideDir(d))
                                         .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                var subNode = new TreeViewItem { Header = Path.GetFileName(sub), Tag = sub };
                parent.Items.Add(subNode);
                AddDirectory(subNode, sub);
            }

            // 3) Add remaining files (skip the .toc we already added)
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
                                          .Where(f => !string.Equals(f, tocPath, StringComparison.OrdinalIgnoreCase))
                                          .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                TryAddFileNode(parent, file);
            }
        }

        private void TryAddFileNode(TreeViewItem parent, string path)
        {
            if (!_treeSeen.Add(path)) return; // dedupe guard
            parent.Items.Add(new TreeViewItem
            {
                Header = Path.GetFileName(path),
                Tag = path
            });
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Copy helpers for builds
        // ─────────────────────────────────────────────────────────────────────────────
        private void CopyDirectorySafe(string src, string dest, string? excludes = null)
        {
            var excludeSet = BuildExcludeSet(excludes);

            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                // skip excluded extensions
                var ext = Path.GetExtension(file);
                if (excludeSet.Contains(ext)) continue;

                var rel = Path.GetRelativePath(src, file);
                var outPath = Path.Combine(dest, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.Copy(file, outPath, overwrite: true);
            }
        }

        private static HashSet<string> BuildExcludeSet(string? excludes)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(excludes))
            {
                var parts = excludes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    var ext = p.Trim();
                    if (!ext.StartsWith(".")) ext = "." + ext;
                    set.Add(ext);
                }
            }
            return set;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Misc
        // ─────────────────────────────────────────────────────────────────────────────
        private void OpenAddonsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set or invalid. Go to Tools → Settings.", "Open AddOns Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Process.Start("explorer.exe", _settings.AddOnsPath);
        }

        private void LiveReload_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) return;

            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set. Tools → Settings.", "Live Reload",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var addonDir = Path.Combine(_settings.AddOnsPath, _project.Name);
                Directory.CreateDirectory(addonDir);
                var flag = Path.Combine(addonDir, "Reload.flag");
                File.WriteAllText(flag, DateTime.UtcNow.ToString("o"));
                Status("Live reload flag written.");
                Log($"Live reload flag: {flag}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Live Reload", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}