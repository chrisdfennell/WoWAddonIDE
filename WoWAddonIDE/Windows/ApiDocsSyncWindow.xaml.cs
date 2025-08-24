using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;

namespace WoWAddonIDE.Windows
{
    public partial class ApiDocsSyncWindow : Window
    {
        public enum Mode { File, Url }

        public HashSet<string> Merged { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public ApiDocsSyncWindow(Mode initial = Mode.File)
        {
            InitializeComponent();
            SourceMode.SelectionChanged += (s, e) =>
            {
                var isUrl = (SourceMode.SelectedIndex == 1);
                FilePanel.Visibility = isUrl ? Visibility.Collapsed : Visibility.Visible;
                UrlPanel.Visibility = isUrl ? Visibility.Visible : Visibility.Collapsed;
            };
            SourceMode.SelectedIndex = (initial == Mode.Url) ? 1 : 0;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON (*.json)|*.json|All Files|*.*"
            };
            if (dlg.ShowDialog(this) == true) FilePath.Text = dlg.FileName;
        }

        private List<string> ParseNames(string json)
        {
            try
            {
                var names = new List<string>();
                if (json.TrimStart().StartsWith("["))
                {
                    var arr = JsonConvert.DeserializeObject<List<dynamic>>(json);
                    foreach (var it in arr)
                    {
                        if (it == null) continue;
                        if (it.name != null) names.Add((string)it.name);
                        else names.Add(it.ToString());
                    }
                }
                return names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Parse Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<string>();
            }
        }

        private string? ReadSource()
        {
            try
            {
                if (SourceMode.SelectedIndex == 0)
                {
                    if (!File.Exists(FilePath.Text)) throw new FileNotFoundException("File not found", FilePath.Text);
                    return File.ReadAllText(FilePath.Text);
                }
                else
                {
                    using var wc = new WebClient();
                    return wc.DownloadString(UrlText.Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private void LoadPreview_Click(object sender, RoutedEventArgs e)
        {
            Preview.Clear();
            var src = ReadSource();
            if (src == null) return;
            var names = ParseNames(src);
            Preview.Text = $"Parsed {names.Count} entries:\n" +
                           string.Join(", ", names.Take(50)) +
                           (names.Count > 50 ? " ..." : "");
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            var src = ReadSource();
            if (src == null) return;
            var names = ParseNames(src);
            Merged = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            DialogResult = true;
        }
    }
}