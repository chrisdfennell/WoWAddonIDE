using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace WoWAddonIDE.Windows
{
    public partial class TocEditorWindow : Window
    {
        private string _tocPath = "";
        private string _root = "";

        public TocEditorWindow(string tocPath)
        {
            InitializeComponent();
            _tocPath = tocPath;
            _root = System.IO.Path.GetDirectoryName(_tocPath)!;
            LoadToc();
        }

        private void LoadToc()
        {
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
            var lines = new System.Collections.Generic.List<string>
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