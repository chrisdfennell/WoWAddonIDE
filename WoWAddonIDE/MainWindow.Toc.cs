using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private void GenerateToc_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            var tocPath = _project!.TocPath;
            var content = TocParser.GenerateDefaultToc(_project.Name, _project.InterfaceVersion ?? "110005");
            foreach (var f in _project.Files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = Path.GetRelativePath(_project.RootPath, f).Replace("\\", "/");
                    content += rel + Environment.NewLine;
                }
            }
            File.WriteAllText(tocPath, content);
            Log("Regenerated .toc");
            OpenFileInTab(tocPath);
        }

        internal IEnumerable<string> ValidateToc(string tocPath)
        {
            var msgs = new List<string>();
            if (!File.Exists(tocPath)) { msgs.Add("TOC not found."); return msgs; }

            var root = Path.GetDirectoryName(tocPath)!;
            var lines = File.ReadAllLines(tocPath);
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requiredKeys = new[] { "Interface", "Title" };
            var foundKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                if (line.StartsWith("##"))
                {
                    var idx = line.IndexOf(':');
                    if (idx > 2)
                    {
                        var key = line.Substring(2, idx - 2).Trim();
                        foundKeys.Add(key);
                    }
                    continue;
                }

                var colon = line.IndexOf(':');
                if (colon > 0 && !line.Contains("\\") && !line.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) && !line.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    var key = line.Substring(0, colon).Trim();
                    foundKeys.Add(key);
                    continue;
                }

                if (line.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) || line.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    var full = Path.GetFullPath(Path.Combine(root, line.Replace('/', Path.DirectorySeparatorChar)));
                    if (!File.Exists(full))
                        msgs.Add($"Missing file (L{i + 1}): {line}");
                    else
                        seenFiles.Add(full);
                }
            }

            foreach (var req in requiredKeys)
                if (!foundKeys.Contains(req))
                    msgs.Add($"Missing required key: {req}");

            if (seenFiles.Count == 0) msgs.Add("TOC lists no Lua/XML files.");

            return msgs;
        }

        // This is the single, authoritative version of this method.
        internal void TryAddToToc(string filePath)
        {
            if (_project == null) return;
            var rel = Path.GetRelativePath(_project.RootPath, filePath).Replace("\\", "/");
            var lines = File.ReadAllLines(_project.TocPath).ToList();
            if (!lines.Any(l => l.Trim().Equals(rel, StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(rel);
                File.WriteAllLines(_project.TocPath, lines);
                Log($"Added to TOC: {rel}");
            }
        }

        private void TocEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null || string.IsNullOrWhiteSpace(_project.TocPath) || !File.Exists(_project.TocPath))
            {
                MessageBox.Show(this, "No TOC found in this project."); return;
            }
            var w = new Windows.TocEditorWindow(_project.TocPath) { Owner = this };
            if (w.ShowDialog() == true)
            {
                Log("TOC saved.");
                OpenFileInTab(_project.TocPath);
            }
        }
    }
}