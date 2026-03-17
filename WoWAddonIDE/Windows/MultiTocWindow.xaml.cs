using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class MultiTocWindow : Window
    {
        public class FlavorRow
        {
            public bool IsChecked { get; set; }
            public string DisplayName { get; set; } = "";
            public string Suffix { get; set; } = "";
            public string InterfaceVersion { get; set; } = "";
            public string Status { get; set; } = "";
        }

        private readonly string _projectRoot;
        private readonly string _addonName;
        private readonly string _baseTocPath;

        public List<FlavorRow> Flavors { get; } = new();
        public List<string> GeneratedFiles { get; } = new();

        public MultiTocWindow(string projectRoot, string addonName, string baseTocPath)
        {
            InitializeComponent();
            _projectRoot = projectRoot;
            _addonName = addonName;
            _baseTocPath = baseTocPath;

            BaseTocLabel.Text = File.Exists(baseTocPath)
                ? Path.GetFileName(baseTocPath)
                : "(not found - will use defaults)";

            // Populate flavor rows
            foreach (var (suffix, displayName, defaultIface) in Constants.WowFlavors)
            {
                var flavorPath = Path.Combine(projectRoot, $"{addonName}_{suffix}.toc");
                var exists = File.Exists(flavorPath);

                Flavors.Add(new FlavorRow
                {
                    IsChecked = !exists, // pre-check flavors that don't exist yet
                    DisplayName = displayName,
                    Suffix = suffix,
                    InterfaceVersion = exists ? ReadInterfaceVersion(flavorPath) : defaultIface,
                    Status = exists ? "Exists" : ""
                });
            }

            FlavorGrid.ItemsSource = Flavors;

            // Show existing TOC files summary
            var existing = TocParser.DiscoverTocFiles(projectRoot, addonName);
            if (existing.Count > 0)
            {
                ExistingLabel.Text = "Existing TOC files: " +
                    string.Join(", ", existing.Select(e => Path.GetFileName(e.Path)));
            }
        }

        private static string ReadInterfaceVersion(string tocPath)
        {
            try
            {
                foreach (var line in File.ReadLines(tocPath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("## Interface:", StringComparison.OrdinalIgnoreCase))
                        return trimmed.Split(':', 2)[1].Trim();
                }
            }
            catch { }
            return "";
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            var selected = Flavors
                .Where(f => f.IsChecked && !string.IsNullOrWhiteSpace(f.InterfaceVersion))
                .Select(f => (f.Suffix, f.InterfaceVersion))
                .ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Check at least one flavor and ensure it has an Interface version.",
                    "Generate TOCs", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var created = TocParser.GenerateFlavorTocs(_projectRoot, _addonName, _baseTocPath, selected);
                GeneratedFiles.AddRange(created);

                // Update status
                foreach (var flavor in Flavors)
                {
                    var path = Path.Combine(_projectRoot, $"{_addonName}_{flavor.Suffix}.toc");
                    if (File.Exists(path))
                        flavor.Status = flavor.IsChecked ? "Generated" : "Exists";
                }
                FlavorGrid.Items.Refresh();

                MessageBox.Show(this,
                    $"Generated {created.Count} flavor TOC file(s):\n" +
                    string.Join("\n", created.Select(Path.GetFileName)),
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
