using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class PkgmetaEditorWindow : Window
    {
        private readonly string _projectRoot;
        private readonly string _addonName;
        private readonly Dictionary<string, System.Windows.Controls.CheckBox> _libCheckboxes = new();

        public string? GeneratedFilePath { get; private set; }

        public PkgmetaEditorWindow(string projectRoot, string addonName)
        {
            InitializeComponent();
            _projectRoot = projectRoot;
            _addonName = addonName;

            PackageAsBox.Text = addonName;

            // Populate library checkboxes
            foreach (var (name, url, tag) in PkgmetaService.KnownLibraries)
            {
                var cb = new System.Windows.Controls.CheckBox
                {
                    Content = $"{name}  ({tag})",
                    Margin = new Thickness(0, 2, 0, 2),
                    ToolTip = url
                };
                _libCheckboxes[name] = cb;
                LibraryCheckboxes.Children.Add(cb);
            }

            // Populate keyword tokens
            KeywordList.ItemsSource = PkgmetaService.KeywordTokens
                .Select(t => new { t.Token, t.Description }).ToList();

            // If .pkgmeta already exists, load it
            var existingPath = Path.Combine(projectRoot, ".pkgmeta");
            if (File.Exists(existingPath))
            {
                var info = PkgmetaService.Parse(existingPath);
                if (!string.IsNullOrWhiteSpace(info.PackageAs))
                    PackageAsBox.Text = info.PackageAs;
                NoLibCheck.IsChecked = info.EnableNoLibCreation;

                foreach (var (extPath, _) in info.Externals)
                {
                    // Match by tag path
                    var match = _libCheckboxes.FirstOrDefault(kv =>
                        PkgmetaService.KnownLibraries.Any(l =>
                            l.Name == kv.Key && l.Tag.Equals(extPath, StringComparison.OrdinalIgnoreCase)));
                    if (match.Value != null)
                        match.Value.IsChecked = true;
                }

                PreviewLabel.Text = $"Existing .pkgmeta found — editing will overwrite.";
            }
            else
            {
                PreviewLabel.Text = "No .pkgmeta exists yet.";
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            var packageAs = PackageAsBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(packageAs))
            {
                MessageBox.Show(this, "Package name is required.", ".pkgmeta",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedLibs = _libCheckboxes
                .Where(kv => kv.Value.IsChecked == true)
                .Select(kv => kv.Key)
                .ToList();

            var content = PkgmetaService.GenerateDefault(packageAs, selectedLibs);

            // Adjust nolib flag
            if (NoLibCheck.IsChecked != true)
                content = content.Replace("enable-nolib-creation: yes", "enable-nolib-creation: no");

            var outPath = Path.Combine(_projectRoot, ".pkgmeta");
            File.WriteAllText(outPath, content);
            GeneratedFilePath = outPath;

            MessageBox.Show(this,
                $".pkgmeta generated at:\n{outPath}\n\n" +
                $"Libraries: {(selectedLibs.Count > 0 ? string.Join(", ", selectedLibs) : "(none)")}\n\n" +
                "Push your repo and the BigWigs packager will use this file automatically.",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
    }
}
