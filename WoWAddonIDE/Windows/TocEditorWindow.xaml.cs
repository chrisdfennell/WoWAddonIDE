using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class TocEditorWindow : Window
    {
        public class TocFileEntry
        {
            public string Label { get; set; } = "";
            public string Path { get; set; } = "";
        }

        private string _tocPath = "";
        private string _root = "";
        private readonly List<TocFileEntry> _tocFiles = new();

        public TocEditorWindow(string tocPath)
        {
            InitializeComponent();
            _tocPath = tocPath;
            _root = System.IO.Path.GetDirectoryName(_tocPath)!;

            // Discover all TOC files for this addon
            var addonName = System.IO.Path.GetFileNameWithoutExtension(_tocPath);
            _tocFiles.AddRange(
                TocParser.DiscoverTocFiles(_root, addonName)
                    .Select(t => new TocFileEntry { Label = $"{t.Label} ({System.IO.Path.GetFileName(t.Path)})", Path = t.Path }));

            if (_tocFiles.Count == 0)
                _tocFiles.Add(new TocFileEntry { Label = System.IO.Path.GetFileName(_tocPath), Path = _tocPath });

            TocSelector.ItemsSource = _tocFiles;

            // Select the entry matching the requested path
            var match = _tocFiles.FindIndex(t => t.Path.Equals(_tocPath, StringComparison.OrdinalIgnoreCase));
            TocSelector.SelectedIndex = match >= 0 ? match : 0;

            // Hide the selector row if there's only one TOC
            if (_tocFiles.Count <= 1)
                TocSelector.Visibility = Visibility.Collapsed;
        }

        private void TocSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TocSelector.SelectedItem is TocFileEntry entry)
            {
                _tocPath = entry.Path;
                LoadToc();
            }
        }

        private void LoadToc()
        {
            Interface.Text = "";
            TitleBox.Text = "";
            Notes.Text = "";
            Files.Items.Clear();

            if (!File.Exists(_tocPath)) return;
            var lines = File.ReadAllLines(_tocPath).ToList();

            foreach (var line in lines)
            {
                var l = line.Trim();
                if (l.StartsWith("##"))
                {
                    var idx = l.IndexOf(':');
                    if (idx > 0)
                    {
                        var key = l.Substring(2, idx - 2).Trim();
                        var val = l.Substring(idx + 1).Trim();
                        if (key.Equals("Interface", StringComparison.OrdinalIgnoreCase)) Interface.Text = val;
                        else if (key.Equals("Title", StringComparison.OrdinalIgnoreCase)) TitleBox.Text = val;
                        else if (key.Equals("Notes", StringComparison.OrdinalIgnoreCase)) Notes.Text = val;
                    }
                }
                else if (l.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) || l.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    Files.Items.Add(l);
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var lines = new List<string>
            {
                "## Interface: " + Interface.Text.Trim(),
                "## Title: " + TitleBox.Text.Trim(),
                "## Notes: " + Notes.Text.Trim()
            };
            foreach (var item in Files.Items) lines.Add(item.ToString()!);

            File.WriteAllLines(_tocPath, lines);
            DialogResult = true;
            Close();
        }

        private void AddFile_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Lua/XML|*.lua;*.xml",
                InitialDirectory = _root
            };
            if (ofd.ShowDialog(this) == true)
            {
                var rel = Path.GetRelativePath(_root, ofd.FileName).Replace("\\", "/");
                Files.Items.Add(rel);
            }
        }

        private void RemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var sel = Files.SelectedItems.Cast<object>().ToList();
            foreach (var s in sel) Files.Items.Remove(s);
        }
    }
}
