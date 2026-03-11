// Windows/ApiDocsBrowserWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class ApiDocsBrowserWindow : Window
    {
        private readonly IReadOnlyList<WoWApiEntry> _allEntries;
        private readonly Action<string>? _insertCallback;
        private WoWApiEntry? _selected;

        // Category grouping
        private readonly List<(string Category, List<WoWApiEntry> Entries)> _categories = new();

        public ApiDocsBrowserWindow()
        {
            InitializeComponent();
            _allEntries = Array.Empty<WoWApiEntry>();
        }

        public ApiDocsBrowserWindow(IReadOnlyList<WoWApiEntry> entries, Action<string>? insertCallback = null)
            : this()
        {
            _allEntries = entries ?? Array.Empty<WoWApiEntry>();
            _insertCallback = insertCallback;
            Loaded += (_, _) => BuildTree();
        }

        private void BuildTree()
        {
            _categories.Clear();
            var groups = _allEntries
                .GroupBy(e => DeriveCategory(e.name))
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
                _categories.Add((g.Key, g.OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase).ToList()));

            RenderTree(null);
        }

        private void RenderTree(string? filter)
        {
            ApiTree.Items.Clear();
            int totalShown = 0;

            foreach (var (category, entries) in _categories)
            {
                var filtered = string.IsNullOrWhiteSpace(filter)
                    ? entries
                    : entries.Where(e =>
                        e.name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        e.signature.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        e.description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                      .ToList();

                if (filtered.Count == 0) continue;

                var catNode = new TreeViewItem
                {
                    Header = $"{category} ({filtered.Count})",
                    Tag = category,
                    IsExpanded = !string.IsNullOrWhiteSpace(filter)
                };

                foreach (var entry in filtered)
                {
                    catNode.Items.Add(new TreeViewItem
                    {
                        Header = entry.name,
                        Tag = entry
                    });
                    totalShown++;
                }

                ApiTree.Items.Add(catNode);
            }

            CountText.Text = $"{totalShown} / {_allEntries.Count} APIs";
            StatusText.Text = $"{_categories.Count(c => ApiTree.Items.OfType<TreeViewItem>().Any(n => (string)n.Tag! == c.Category))} categories";
        }

        private static string DeriveCategory(string apiName)
        {
            // C_Namespace.Method -> "C_Namespace"
            var dotIdx = apiName.IndexOf('.');
            if (dotIdx > 0) return apiName.Substring(0, dotIdx);

            // Frame:Method -> "Frame Methods"
            var colonIdx = apiName.IndexOf(':');
            if (colonIdx > 0) return apiName.Substring(0, colonIdx) + " Methods";

            // UnitXxx -> "Unit"
            if (apiName.StartsWith("Unit", StringComparison.Ordinal)) return "Unit";
            if (apiName.StartsWith("Get", StringComparison.Ordinal)) return "Get Functions";
            if (apiName.StartsWith("Set", StringComparison.Ordinal)) return "Set Functions";
            if (apiName.StartsWith("Is", StringComparison.Ordinal)) return "Is/Has Functions";
            if (apiName.StartsWith("EJ_", StringComparison.Ordinal)) return "Encounter Journal";

            // Standard Lua
            if (apiName.StartsWith("string.", StringComparison.Ordinal) ||
                apiName.StartsWith("table.", StringComparison.Ordinal) ||
                apiName.StartsWith("math.", StringComparison.Ordinal))
                return "Lua Standard Library";

            // Lua builtins
            var luaBuiltins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "print", "pairs", "ipairs", "type", "tostring", "tonumber",
                "pcall", "xpcall", "error", "select", "unpack", "assert"
            };
            if (luaBuiltins.Contains(apiName)) return "Lua Standard Library";

            return "Other";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RenderTree(SearchBox.Text);
        }

        private void ApiTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ApiTree.SelectedItem is TreeViewItem tvi && tvi.Tag is WoWApiEntry entry)
            {
                _selected = entry;
                DetailName.Text = entry.name;
                DetailCategory.Text = DeriveCategory(entry.name);
                DetailSignature.Text = entry.signature;
                DetailDescription.Text = entry.description;
                DetailCategoryFull.Text = DeriveCategory(entry.name);
            }
        }

        private void CopyName_Click(object sender, RoutedEventArgs e)
        {
            if (_selected != null)
                try { Clipboard.SetText(_selected.name); } catch { }
        }

        private void CopySignature_Click(object sender, RoutedEventArgs e)
        {
            if (_selected != null)
                try { Clipboard.SetText(_selected.signature); } catch { }
        }

        private void InsertAtCursor_Click(object sender, RoutedEventArgs e)
        {
            if (_selected != null && _insertCallback != null)
            {
                _insertCallback(_selected.name);
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        }
    }
}
