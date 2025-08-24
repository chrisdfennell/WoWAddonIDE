using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace WoWAddonIDE.Windows
{
    public partial class CommandPaletteWindow : Window
    {
        private class CommandItem
        {
            public string Title { get; set; } = "";
            public string? Subtitle { get; set; }
            public Action Execute { get; set; } = () => { };
        }

        private readonly List<CommandItem> _all = new();
        private List<CommandItem> _filtered = new();

        public CommandPaletteWindow()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                BuildCatalog();
                ApplyFilter("");
                QueryBox.Focus();
            };
        }

        public CommandPaletteWindow(MainWindow owner) : this()
        {
            Owner = owner;
        }

        private void BuildCatalog()
        {
            if (Owner is not MainWindow main) return;

            // Helper that invokes private/public event handlers safely
            void Exec(string handlerName)
            {
                var mi = main.GetType().GetMethod(handlerName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi == null) return;

                // Most of your handlers are (object sender, RoutedEventArgs e)
                var parms = mi.GetParameters();
                object?[] args = parms.Length == 2
                    ? new object?[] { this, new RoutedEventArgs() }
                    : Array.Empty<object?>();

                main.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { mi.Invoke(main, args); } catch { /* swallow */ }
                }));
            }

            _all.Clear();
            _all.AddRange(new[]
            {
                // Project / Build
                new CommandItem { Title = "Build", Subtitle = "Copy to AddOns", Execute = () => Exec("Build_Click") },
                new CommandItem { Title = "Build Zip", Subtitle = "Create package zip", Execute = () => Exec("BuildZip_Click") },
                new CommandItem { Title = "Live Reload", Subtitle = "Write Reload.flag", Execute = () => Exec("LiveReload_Click") },
                new CommandItem { Title = "Open AddOns Folder", Execute = () => Exec("OpenAddOnsFolder_Click") },
                new CommandItem { Title = "Open Staging Folder", Execute = () => Exec("OpenStagingFolder_Click") },

                // Edit / Nav
                new CommandItem { Title = "Find in Files", Subtitle = "Ctrl+Shift+F", Execute = () => Exec("FindInFiles_Click") },
                new CommandItem { Title = "Go To Symbol", Execute = () => Exec("GoToSymbol_Click") },
                new CommandItem { Title = "Show Diff", Subtitle = "Editor vs Disk", Execute = () => Exec("ShowDiff_Click") },
                new CommandItem { Title = "TOC Editor", Execute = () => Exec("TocEditor_Click") },

                // Git
                new CommandItem { Title = "Git: Status", Execute = () => Exec("GitStatus_Click") },
                new CommandItem { Title = "Git: Commit", Execute = () => Exec("GitCommit_Click") },
                new CommandItem { Title = "Git: Pull", Execute = () => Exec("GitPull_Click") },
                new CommandItem { Title = "Git: Push", Execute = () => Exec("GitPush_Click") },
                new CommandItem { Title = "Git: Sync", Execute = () => Exec("GitSync_Click") },
                new CommandItem { Title = "Git: Blame (Active File)", Execute = () => Exec("GitBlameActive_Click") },
                new CommandItem { Title = "Git: History (Active File)", Execute = () => Exec("GitHistoryActive_Click") },
                new CommandItem { Title = "Git: Merge Helper", Execute = () => Exec("GitMergeHelper_Click") },
                new CommandItem { Title = "GitHub: Publish Release", Execute = () => Exec("GitPublishRelease_Click") },

                // Theme
                new CommandItem { Title = "Theme: System", Execute = () => Exec("ThemeSystem_Click") },
                new CommandItem { Title = "Theme: Light",  Execute = () => Exec("ThemeLight_Click") },
                new CommandItem { Title = "Theme: Dark",   Execute = () => Exec("ThemeDark_Click") },

                // API
                new CommandItem { Title = "API: Import JSON", Execute = () => Exec("ApiDocsImportFile_Click") },
                new CommandItem { Title = "API: Reload",      Execute = () => Exec("ApiDocsReload_Click") },

                // Misc
                new CommandItem { Title = "Settings", Subtitle = "Tools → Git/GitHub Settings…", Execute = () => Exec("Settings_Click") },
                new CommandItem { Title = "About", Execute = () => Exec("About_Click") },
            });
        }

        private void ApplyFilter(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                _filtered = _all.Take(100).ToList();
            }
            else
            {
                var q = query.Trim();
                _filtered = _all
                    .Select(c => new { Score = Score(c, q), Item = c })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Item.Title)
                    .Select(x => x.Item)
                    .Take(100)
                    .ToList();
            }

            ResultsList.ItemsSource = _filtered;
            if (_filtered.Count > 0)
            {
                ResultsList.SelectedIndex = 0;
                ResultsList.ScrollIntoView(_filtered[0]);
            }
        }

        private static int Score(CommandItem c, string q)
        {
            int s = 0;
            foreach (var part in q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                bool hit = (c.Title?.IndexOf(part, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                           || (c.Subtitle?.IndexOf(part, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
                if (!hit) return 0;
                s += 10;
            }
            return s;
        }

        // ---------- Events ----------
        private void QueryBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => ApplyFilter(QueryBox.Text);

        private void QueryBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (ResultsList.Items.Count > 0)
                {
                    int i = Math.Max(0, ResultsList.SelectedIndex) + 1;
                    if (i >= ResultsList.Items.Count) i = ResultsList.Items.Count - 1;
                    ResultsList.SelectedIndex = i;
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (ResultsList.Items.Count > 0)
                {
                    int i = Math.Max(0, ResultsList.SelectedIndex - 1);
                    ResultsList.SelectedIndex = i;
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ExecuteSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }

        private void ResultsList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteSelection();
                e.Handled = true;
            }
        }

        private void ResultsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => ExecuteSelection();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void ExecuteSelection()
        {
            if (ResultsList.SelectedItem is CommandItem cmd)
            {
                DialogResult = true;
                Close();
                try { cmd.Execute(); } catch { /* ignore */ }
            }
        }
    }
}