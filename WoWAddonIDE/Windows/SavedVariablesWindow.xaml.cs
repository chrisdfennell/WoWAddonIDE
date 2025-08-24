using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ookii.Dialogs.Wpf;                 // for VistaFolderBrowserDialog
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Windows
{
    public partial class SavedVariablesWindow : Window
    {
        private readonly IDESettings _settings;

        // top-level SavedVariables file paths (we filter & render from this)
        private readonly List<string> _filePaths = new();
        private string _filter = string.Empty;

        public SavedVariablesWindow()
        {
            InitializeComponent();
            _settings = new IDESettings(); // harmless default
            Loaded += (_, __) => LoadFiles();
        }

        // Overload required by your call site
        public SavedVariablesWindow(IDESettings settings) : this()
        {
            _settings = settings ?? new IDESettings();
        }

        private string SavedVarsRoot =>
            GuessSavedVariablesRoot(_settings?.AddOnsPath) ?? "";

        private static string? GuessSavedVariablesRoot(string? addonsPath)
        {
            if (string.IsNullOrWhiteSpace(addonsPath)) return null;

            try
            {
                // …\Interface\AddOns -> go up to the WoW flavor directory (_retail_, _classic_, _classic_era, etc.)
                var addons = new DirectoryInfo(addonsPath);
                var interfaceDir = addons.Parent;    // Interface
                var flavorDir = interfaceDir?.Parent;// _retail_ / _classic_ / _classic_era / etc
                var wowRoot = flavorDir?.Parent;     // World of Warcraft root

                IEnumerable<string> candidates = new[]
                {
                    Path.Combine(wowRoot?.FullName ?? "", "_retail_"),
                    Path.Combine(wowRoot?.FullName ?? "", "_classic_"),
                    Path.Combine(wowRoot?.FullName ?? "", "_classic_era"),
                    Path.Combine(wowRoot?.FullName ?? "", "_wrath_"),
                    flavorDir?.FullName ?? ""
                }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var flavor in candidates)
                {
                    var accountRoot = Path.Combine(flavor, "WTF", "Account");
                    if (!Directory.Exists(accountRoot)) continue;

                    // prefer the account that actually has SavedVariables
                    foreach (var acc in Directory.EnumerateDirectories(accountRoot))
                    {
                        var sv = Path.Combine(acc, "SavedVariables");
                        if (Directory.Exists(sv))
                            return sv;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        // ---------------- UI events (wired in XAML) ----------------

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void BrowseRoot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new VistaFolderBrowserDialog
                {
                    Description = "Pick a SavedVariables folder (…\\WTF\\Account\\<Account>\\SavedVariables)",
                    UseDescriptionForTitle = true,
                    SelectedPath = string.IsNullOrWhiteSpace(RootPathBox.Text) ? SavedVarsRoot : RootPathBox.Text
                };
                if (dlg.ShowDialog(this) == true)
                {
                    RootPathBox.Text = dlg.SelectedPath;
                    LoadFilesFrom(RootPathBox.Text);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Browse", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadFiles();

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filter = FilterBox.Text ?? string.Empty;
            RenderFileList(); // filter the top-level file list
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e) => SetExpandCollapse(FilesTree, expand: true);

        private void CollapseAll_Click(object sender, RoutedEventArgs e) => SetExpandCollapse(FilesTree, expand: false);

        // On-demand parse when a file (or value node) is selected
        private async void FilesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FilesTree.SelectedItem is not TreeViewItem tvi) return;

            // File node (Tag = string path)
            if (tvi.Tag is string path && File.Exists(path))
            {
                // don’t re-parse if already populated
                if (tvi.Items.Count == 0 || (tvi.Items.Count == 1 && tvi.Items[0] is TextBlock))
                {
                    tvi.Items.Clear();
                    tvi.Items.Add(new TextBlock { Text = "Loading…", Opacity = 0.7 });

                    try
                    {
                        StatusText.Text = $"Loading {System.IO.Path.GetFileName(path)}…";

                        var fi = new FileInfo(path);
                        if (fi.Length > 25 * 1024 * 1024)
                        {
                            StatusText.Text = "File too large (>25MB). Use a filter or open externally.";
                            tvi.Items.Clear();
                            tvi.Items.Add(new TextBlock { Text = "(skipped – file > 25MB)" });
                            return;
                        }

                        string text = await File.ReadAllTextAsync(path).ConfigureAwait(false);

                        var tables = await Task.Run(() =>
                            SimpleLuaTableScanner.ExtractTopTables(text)
                        ).ConfigureAwait(false);

                        Dispatcher.Invoke(() =>
                        {
                            tvi.Items.Clear();
                            foreach (var kv in tables)
                            {
                                var node = new TreeViewItem
                                {
                                    Header = kv.Key,
                                    Tag = new ValueNode
                                    {
                                        DisplayPath = $"{System.IO.Path.GetFileName(path)}::{kv.Key}",
                                        Value = kv.Value
                                    }
                                };
                                AddDictionaryToTree(node, kv.Value);
                                node.IsExpanded = false;
                                tvi.Items.Add(node);
                            }

                            StatusText.Text = $"{System.IO.Path.GetFileName(path)} — {tables.Count} top-level tables.";
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            tvi.Items.Clear();
                            tvi.Items.Add(new TextBlock { Text = $"(error: {ex.Message})", Foreground = System.Windows.Media.Brushes.OrangeRed });
                            StatusText.Text = ex.Message;
                        });
                    }
                }

                SelectedPathBox.Text = path;
                ValueBox.Text = ""; // clear preview until a value node is clicked
                return;
            }

            // Value/Table node (Tag = ValueNode)
            if (tvi.Tag is ValueNode vn)
            {
                SelectedPathBox.Text = vn.DisplayPath;
                ValueBox.Text = RenderValueForPreview(vn.Value);
            }
        }

        // Recursively add dictionary/list leaves as tree nodes
        private static void AddDictionaryToTree(ItemsControl parent, object value)
        {
            switch (value)
            {
                case IDictionary<string, object?> dict:
                    foreach (var kv in dict.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        var node = new TreeViewItem
                        {
                            Header = kv.Key,
                            Tag = new ValueNode
                            {
                                DisplayPath = $"{(parent as TreeViewItem)?.Header}.{kv.Key}",
                                Value = kv.Value
                            }
                        };
                        parent.Items.Add(node);
                        AddDictionaryToTree(node, kv.Value!);
                    }
                    break;

                case IList list:
                    for (int i = 0; i < list.Count; i++)
                    {
                        var node = new TreeViewItem
                        {
                            Header = $"[{i + 1}]",
                            Tag = new ValueNode
                            {
                                DisplayPath = $"{(parent as TreeViewItem)?.Header}[{i + 1}]",
                                Value = list[i]
                            }
                        };
                        parent.Items.Add(node);
                        AddDictionaryToTree(node, list[i]!);
                    }
                    break;

                default:
                    parent.Items.Add(new TreeViewItem
                    {
                        Header = value is null ? "(nil)" : value.ToString(),
                        Tag = new ValueNode
                        {
                            DisplayPath = (parent as TreeViewItem)?.Header?.ToString() ?? "",
                            Value = value
                        }
                    });
                    break;
            }
        }

        private sealed class ValueNode
        {
            public string DisplayPath { get; set; } = "";
            public object? Value { get; set; }
        }

        private static string RenderValueForPreview(object? v)
        {
            return v switch
            {
                null => "(nil)",
                IDictionary<string, object?> dict => "{ " + string.Join(", ", dict.Keys.OrderBy(k => k)) + " }",
                IList list => $"{{ array, {list.Count} items }}",
                string s => s,
                bool b => b ? "true" : "false",
                double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
                float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => v?.ToString() ?? ""
            };
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            var text = SelectedPathBox.Text ?? "";
            if (text.Length == 0) return;
            try { Clipboard.SetText(text); } catch { /* ignore */ }
        }

        private void CopyValue_Click(object sender, RoutedEventArgs e)
        {
            var text = ValueBox.Text ?? "";
            if (text.Length == 0) return;
            try { Clipboard.SetText(text); } catch { /* ignore */ }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ---------------- Load + Render top-level files ----------------

        private void LoadFiles()
        {
            RootPathBox.Text = SavedVarsRoot;
            _filePaths.Clear();

            FilesTree.Items.Clear();
            SelectedPathBox.Text = "";
            ValueBox.Text = "";

            if (string.IsNullOrWhiteSpace(SavedVarsRoot) || !Directory.Exists(SavedVarsRoot))
            {
                StatusText.Text = "SavedVariables folder not found. Set AddOns Path in Tools → Settings or click Browse…";
                return;
            }

            _filePaths.AddRange(Directory.EnumerateFiles(SavedVarsRoot, "*.lua", SearchOption.TopDirectoryOnly)
                                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));

            RenderFileList();
            StatusText.Text = $"Found {_filePaths.Count} files.";
        }

        private void LoadFilesFrom(string? root)
        {
            _filePaths.Clear();

            FilesTree.Items.Clear();
            SelectedPathBox.Text = "";
            ValueBox.Text = "";

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                StatusText.Text = "SavedVariables folder not found. Check Tools → Settings → AddOns Path, or click Browse…";
                return;
            }

            _filePaths.AddRange(Directory.EnumerateFiles(root, "*.lua", SearchOption.TopDirectoryOnly)
                                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
            RenderFileList();
            StatusText.Text = $"Found {_filePaths.Count} files.";
        }

        private void RenderFileList()
        {
            FilesTree.Items.Clear();

            IEnumerable<string> items = _filePaths;
            if (!string.IsNullOrWhiteSpace(_filter))
                items = items.Where(p => Path.GetFileName(p)
                                   .IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0);

            foreach (var path in items)
            {
                var tvi = new TreeViewItem
                {
                    Header = System.IO.Path.GetFileName(path),
                    Tag = path
                };
                // add a dummy so it shows an item to replace on load
                tvi.Items.Add(new TextBlock { Text = "(select to load)", Opacity = 0.55 });
                FilesTree.Items.Add(tvi);
            }
        }

        private static void SetExpandCollapse(ItemsControl root, bool expand)
        {
            foreach (var tvi in EnumerateTree(root))
                tvi.IsExpanded = expand;
        }

        private static IEnumerable<TreeViewItem> EnumerateTree(ItemsControl root)
        {
            foreach (var obj in root.Items)
            {
                if (obj is TreeViewItem tvi)
                {
                    yield return tvi;
                    foreach (var child in EnumerateTree(tvi))
                        yield return child;
                }
            }
        }

        // ---------------- Minimal Lua table scanner ----------------
        private static class SimpleLuaTableScanner
        {
            public static Dictionary<string, object?> ExtractTopTables(string lua)
            {
                var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                int i = 0;
                while (i < lua.Length)
                {
                    SkipSpace(lua, ref i);
                    // NAME =
                    var name = ReadIdentifier(lua, ref i);
                    if (string.IsNullOrEmpty(name)) { i++; continue; }
                    SkipSpace(lua, ref i);
                    if (!TryRead(lua, ref i, '=')) continue;
                    SkipSpace(lua, ref i);
                    if (TryRead(lua, ref i, '{'))
                    {
                        var val = ReadTable(lua, ref i);
                        result[name] = val;
                    }
                    i++;
                }
                return result;
            }

            private static object? ReadValue(string s, ref int i)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length) return null;
                if (TryRead(s, ref i, '{')) return ReadTable(s, ref i);
                if (s[i] == '"' || s[i] == '\'') return ReadString(s, ref i);
                if (StartsWith(s, i, "true")) { i += 4; return true; }
                if (StartsWith(s, i, "false")) { i += 5; return false; }
                if (StartsWith(s, i, "nil")) { i += 3; return null; }

                int start = i;
                while (i < s.Length && ("+-0123456789.eE".IndexOf(s[i]) >= 0)) i++;
                if (double.TryParse(s.Substring(start, i - start), out var num))
                    return num;

                return ReadIdentifier(s, ref i);
            }

            private static IDictionary<string, object?> ReadTable(string s, ref int i)
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                var list = new List<object?>();
                bool isArray = true;

                while (i < s.Length)
                {
                    SkipSpace(s, ref i);
                    if (TryRead(s, ref i, '}')) break;

                    if (TryRead(s, ref i, '['))
                    {
                        SkipSpace(s, ref i);
                        var idxVal = ReadValue(s, ref i);
                        SkipSpace(s, ref i);
                        TryRead(s, ref i, ']');
                        SkipSpace(s, ref i);
                        TryRead(s, ref i, '=');
                        var v = ReadValue(s, ref i);
                        dict[idxVal?.ToString() ?? ""] = v;
                        isArray = false;
                    }
                    else
                    {
                        int save = i;
                        var key = ReadIdentifier(s, ref i);
                        SkipSpace(s, ref i);
                        if (!string.IsNullOrEmpty(key) && TryRead(s, ref i, '='))
                        {
                            var v = ReadValue(s, ref i);
                            dict[key] = v;
                            isArray = false;
                        }
                        else
                        {
                            i = save;
                            var v = ReadValue(s, ref i);
                            list.Add(v);
                        }
                    }

                    SkipSpace(s, ref i);
                    TryRead(s, ref i, ',');
                }

                return isArray ? new Dictionary<string, object?> { ["$array"] = list } : dict;
            }

            private static string ReadString(string s, ref int i)
            {
                char q = s[i++];
                var start = i;
                while (i < s.Length && s[i] != q)
                {
                    if (s[i] == '\\' && i + 1 < s.Length) i += 2;
                    else i++;
                }
                var str = s.Substring(start, Math.Max(0, i - start));
                if (i < s.Length && s[i] == q) i++;
                return str;
            }

            private static string ReadIdentifier(string s, ref int i)
            {
                int start = i;
                if (i < s.Length && (char.IsLetter(s[i]) || s[i] == '_'))
                {
                    i++;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    return s.Substring(start, i - start);
                }
                return "";
            }

            private static void SkipSpace(string s, ref int i)
            {
                while (i < s.Length)
                {
                    if (char.IsWhiteSpace(s[i])) { i++; continue; }
                    if (s[i] == '-' && i + 1 < s.Length && s[i + 1] == '-')
                    {
                        i += 2;
                        while (i < s.Length && s[i] != '\n') i++;
                        continue;
                    }
                    break;
                }
            }

            private static bool TryRead(string s, ref int i, char c)
                => i < s.Length && s[i] == c ? (++i >= 0) : false;

            private static bool StartsWith(string s, int i, string token)
                => i + token.Length <= s.Length &&
                   string.Compare(s, i, token, 0, token.Length, StringComparison.Ordinal) == 0;
        }
    }
}
