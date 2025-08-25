using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private class ApiEntry
        {
            public string name { get; set; } = "";
            public string signature { get; set; } = "";
            public string description { get; set; } = "";
        }

        private void LoadApiDocs()
        {
            try
            {
                using var s = Application.GetResourceStream(new Uri("Resources/wow_api.json", UriKind.Relative))?.Stream
                                ?? Application.GetResourceStream(new Uri($"/{Assembly.GetExecutingAssembly().GetName().Name};component/Resources/wow_api.json", UriKind.Relative))?.Stream;

                if (s == null)
                {
                    Log("wow_api.json not found as Resource; hover docs disabled.");
                    return;
                }

                using var sr = new StreamReader(s);
                var json = sr.ReadToEnd();
                var arr = JsonConvert.DeserializeObject<List<ApiEntry>>(json) ?? new List<ApiEntry>();

                _apiDocs = arr
                    .Where(a => !string.IsNullOrWhiteSpace(a.name))
                    .GroupBy(a => a.name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                Log($"Loaded {_apiDocs.Count} API entries for hover docs.");
            }
            catch (Exception ex)
            {
                Log($"Failed to load wow_api.json: {ex.Message}");
            }
        }

        private void ApiDocsReload_Click(object sender, RoutedEventArgs e)
        {
            // Reload the embedded default Resources/wow_api.json
            LoadApiDocs();
            _completion.SetApiNames(_apiDocs.Keys);
            Status($"API docs reloaded: {_apiDocs.Count} entries.");
            Log("API docs reloaded from embedded resource.");
        }

        private void ApiDocsImportFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import WoW API Docs (JSON)",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                CheckFileExists = true
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var items = JsonConvert.DeserializeObject<List<ApiEntry>>(json) ?? new List<ApiEntry>();
                MergeApiDocs(items);
                Log($"API docs merged from file: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Import API Docs", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ApiDocsImportUrl_Click(object sender, RoutedEventArgs e)
        {
            var url = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter a URL to a WoW API JSON file:", "Import API Docs (URL)", "https://example.com/wow_api.json");
            if (string.IsNullOrWhiteSpace(url)) return;

            try
            {
                using var http = new HttpClient();
                var json = await http.GetStringAsync(url);
                var items = JsonConvert.DeserializeObject<List<ApiEntry>>(json) ?? new List<ApiEntry>();
                MergeApiDocs(items);
                Log($"API docs merged from URL: {url}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Import API Docs (URL)", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MergeApiDocs(IEnumerable<ApiEntry> entries)
        {
            int before = _apiDocs.Count;

            foreach (var en in entries)
            {
                if (!string.IsNullOrWhiteSpace(en.name))
                    _apiDocs[en.name] = en; // overwrite/merge by name
            }

            _completion.SetApiNames(_apiDocs.Keys);
            Status($"API docs merged: {before} → {_apiDocs.Count}");
        }

        private async void ApiDocsImportFromWow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addons = ThemeManager.Settings.AddOnsPath;
                if (string.IsNullOrWhiteSpace(addons) || !Directory.Exists(addons))
                {
                    MessageBox.Show(this,
                        "AddOns Path is not set or invalid.\n\nTools → Settings → AddOns Path",
                        "Import WoW API", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var entries = await WowApiImporter.ImportFromWowAsync(addons);
                _completion.SetApiNames(_apiDocs.Keys);

                MessageBox.Show(this, $"Loaded {entries.Count} API entries.",
                    "WoW API", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "WoW API Import",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
