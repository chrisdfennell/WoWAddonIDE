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
                MessageBox.Show(this, ex.Message, "New Project Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show(this, "No .toc found in that directory.", "Open Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                RefreshProjectTree();
                Status($"Opened project: {_project.Name}");
                PathText.Text = $"Project: {_project.RootPath}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open Project Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
            var name = Interaction.InputBox("New XML filename (without path):", "Add XML File", "Frame.xml");
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

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            SaveAll_Click(sender, e);

            foreach (var p in LuaLint.Pass(_project!)) Log(p);

            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set or invalid. Use Tools > Settings.", "Build", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        "You can enable it in Tools > Settings (not recommended).",
                        "Build Skipped", MessageBoxButton.OK, MessageBoxImage.Information);
                    Log("Build skipped: project resides inside AddOns.");
                    return;
                }

                if (string.Equals(src, target, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "Source and target are the same folder. Aborting.", "Build Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(this, ex.Message, "Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show(this, "Source and target are the same folder. Pick a different destination.", "Build to Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
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
                MessageBox.Show(this, ex.Message, "Build Zip Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { /* optional */ }

        private void ProjectTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProjectTree.SelectedItem is TreeViewItem tvi && tvi.Tag is string path && File.Exists(path))
            {
                OpenFileInTab(path);
            }
        }

        private void RefreshProjectTree()
        {
            ProjectTree.Items.Clear();
            if (_project == null) return;

            var rootItem = new TreeViewItem
            {
                Header = _project.Name,
                Tag = _project.RootPath,
                IsExpanded = true
            };

            string? primaryToc = File.Exists(_project.TocPath)
                ? Path.GetFullPath(_project.TocPath)
                : null;

            if (primaryToc != null)
            {
                rootItem.Items.Add(new TreeViewItem
                {
                    Header = Path.GetFileName(primaryToc),
                    Tag = primaryToc
                });
            }

            void AddDir(TreeViewItem parent, string dir)
            {
                IEnumerable<string> subdirs;
                try { subdirs = Directory.GetDirectories(dir); }
                catch { return; }

                foreach (var sub in subdirs.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(sub);

                    if (name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals(".vs", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var di = new DirectoryInfo(sub);
                        if ((di.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                            continue;
                    }
                    catch { /* ignore */ }

                    var node = new TreeViewItem
                    {
                        Header = name,
                        Tag = sub,
                        IsExpanded = false
                    };
                    AddDir(node, sub);
                    parent.Items.Add(node);
                }

                IEnumerable<string> files;
                try { files = Directory.GetFiles(dir); }
                catch { return; }

                foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext is ".tga" or ".png" or ".jpg") continue;

                    parent.Items.Add(new TreeViewItem
                    {
                        Header = Path.GetFileName(file),
                        Tag = file
                    });
                }
            }

            AddDir(rootItem, _project.RootPath);
            ProjectTree.Items.Add(rootItem);
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
            try { Clipboard.SetText(p); } catch { }
        }

        private void ProjectTree_CopyRelPath_Click(object s, RoutedEventArgs e)
        {
            var p = SelectedTreePath(); if (p == null) return;
            try
            {
                var rel = (_project != null) ? Path.GetRelativePath(_project.RootPath, p) : p;
                Clipboard.SetText(rel);
            }
            catch { }
        }

        private void CopyDirectorySafe(string src, string dest, string? excludes = null)
        {
            var excludeSet = BuildExcludeSet(excludes);
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (excludeSet.Contains(ext)) continue;

                var rel = Path.GetRelativePath(src, file);
                var outPath = Path.Combine(dest, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.Copy(file, outPath, true);
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

        private void OpenAddonsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set or invalid. Go to Tools > Settings.", "Open AddOns Folder",
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
                MessageBox.Show(this, "AddOns path is not set. Tools > Settings.", "Live Reload",
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
                MessageBox.Show(this, ex.Message, "Live Reload Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}