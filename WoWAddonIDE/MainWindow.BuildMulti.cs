// MainWindow.BuildMulti.cs
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using WoWAddonIDE.Windows;

namespace WoWAddonIDE
{
    public partial class MainWindow
    {
        private void BuildMultiZip_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            SaveAll_Click(sender, e);

            try
            {
                var stagingRoot = string.IsNullOrWhiteSpace(_settings.StagingPath)
                    ? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WoWAddonIDE", "Staging")
                    : _settings.StagingPath;
                Directory.CreateDirectory(stagingRoot);

                var w = new BuildTargetsWindow(_project!.InterfaceVersion) { Owner = this };
                if (w.ShowDialog() != true) return;

                foreach (var target in w.SelectedTargets())
                {
                    var tempFolder = Path.Combine(stagingRoot, "_multi_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempFolder);

                    var stageTarget = Path.Combine(tempFolder, _project!.Name);
                    CopyDirectorySafe(_project!.RootPath, stageTarget, _settings.PackageExcludes);

                    // Patch TOC Interface in the staged copy
                    var tocPath = Path.Combine(stageTarget, System.IO.Path.GetFileName(_project!.TocPath));
                    if (File.Exists(tocPath)) UpdateTocInterface(tocPath, target.Interface);

                    var zipName = $"{_project!.Name}-{target.Suffix}-{DateTime.Now:yyyyMMdd-HHmm}.zip";
                    var zipPath = Path.Combine(stagingRoot, zipName);
                    if (File.Exists(zipPath)) File.Delete(zipPath);

                    ZipFile.CreateFromDirectory(tempFolder, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
                    try { Directory.Delete(tempFolder, true); } catch { /* ignore */ }

                    Log($"Multi-Zip: {zipName}");
                }

                Status("Build Multi-Zip completed.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Build Multi-Zip", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void UpdateTocInterface(string tocPath, string interfaceNum)
        {
            var lines = File.ReadAllLines(tocPath).ToList();
            bool replaced = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i].TrimStart();
                if (l.StartsWith("## Interface", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = "## Interface: " + interfaceNum;
                    replaced = true;
                    break;
                }
            }
            if (!replaced) lines.Insert(0, "## Interface: " + interfaceNum);
            File.WriteAllLines(tocPath, lines);
        }
    }
}