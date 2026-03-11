using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private AddonProject? _project;
        private readonly string _settingsPath;
        private IDESettings _settings = new();

        // Editor toggles (session-scoped)
        private bool _wordWrap = false;
        private bool _showInvisibles = false;

        private CompletionService _completion;

        // Hover docs (from Resources/wow_api.json)
        private Dictionary<string, ApiEntry> _apiDocs = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, List<SymbolService.SymbolLocation>> _symbolIndex = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _symbolIndexBuilt = DateTime.MinValue;

        // Zoom baseline
        private double _defaultEditorFontSize = 0;

        // Closed tab history for Ctrl+Shift+T
        private readonly Stack<string> _closedTabPaths = new();

        // Sidebar toggle state
        private bool _sidebarVisible = true;
        private GridLength _sidebarSavedWidth = new GridLength(280);

        public MainWindow()
        {
            InitializeComponent();
            InitializeLogging();

            // ===== your original startup steps =====
            Formatter_Autodetect();
            Recent_Init();
            Autosave_Init();
            Formatter_MigrateArgsIfNeeded();

            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WoWAddonIDE", "settings.json");

            _settings = LoadSettings();

            // Initialize completion/highlighting (loads wow_api.json for completion)
            _completion = new CompletionService();

            // Initialize Lua REPL
            Repl_Init();

            // Register Lua & TOC highlighting
            EnsureLuaHighlightRegistered();
            EnsureTocHighlightRegistered();

            // Load API docs for hover tooltips
            LoadApiDocs();

            Status("Ready");
            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath))
            {
                _settings.AddOnsPath = DetectAddOnsPath();
                SaveSettings();
            }

            PathText.Text = string.IsNullOrWhiteSpace(_settings.AddOnsPath)
                ? "AddOns Path: (not set) — Tools > Settings..."
                : $"AddOns Path: {_settings.AddOnsPath}";

            // THEME: initialize + apply + listen for changes
            ThemeManager.Initialize(_settings);
            ThemeManager.Persist = SaveSettings;
            ThemeManager.ApplyTheme(_settings.ThemeMode); // System/Light/Dark
            ThemeManager.ThemeChanged += () =>
            {
                foreach (TabItem tab in EditorTabs.Items)
                {
                    var ed = FindEditorIn(tab.Content);
                    if (ed != null)
                    {
                        ThemeManager.ApplyToEditor(ed);
                        if (ed.SyntaxHighlighting != null)
                            RetintHighlighting(ed.SyntaxHighlighting, IsDarkThemeActive());
                    }
                }
                // Re-theme minimap editor
                if (_minimapEditor != null)
                {
                    ThemeManager.ApplyToEditor(_minimapEditor);
                    _minimapEditor.FontSize = 1;
                    _minimapEditor.ShowLineNumbers = false;
                }
            };

            // Folding + Minimap initialization
            Folding_Init();
            Minimap_Init();

            // --- Shortcut binder helpers (unique names to avoid collisions) ---
            void Map(Key key, ModifierKeys mods, Action action)
                => this.InputBindings.Add(new KeyBinding(new RelayCommand(_ => action()), new KeyGesture(key, mods)));
            void Map0(Key key, Action action)
                => this.InputBindings.Add(new KeyBinding(new RelayCommand(_ => action()), new KeyGesture(key)));

            // ===== FILE =====
            Map(Key.N, ModifierKeys.Control, () => NewProject_Click(this, new RoutedEventArgs()));               // Ctrl+N
            Map(Key.O, ModifierKeys.Control, () => OpenProject_Click(this, new RoutedEventArgs()));              // Ctrl+O
            Map(Key.S, ModifierKeys.Control, SaveActiveTabShortcut);                                             // Ctrl+S
            Map(Key.S, ModifierKeys.Control | ModifierKeys.Alt, () => SaveAll_Click(this, new RoutedEventArgs()));// Ctrl+Alt+S
            Map(Key.S, ModifierKeys.Control | ModifierKeys.Shift, SaveAsActiveTab);                              // Ctrl+Shift+S
            Map(Key.W, ModifierKeys.Control, CloseActiveTab);                                                    // Ctrl+W
            Map(Key.W, ModifierKeys.Control | ModifierKeys.Shift, CloseAllTabs);                                 // Ctrl+Shift+W
            Map(Key.T, ModifierKeys.Control | ModifierKeys.Shift, ReopenLastClosedTab);                          // Ctrl+Shift+T
            Map(Key.N, ModifierKeys.Control | ModifierKeys.Alt, () => NewFile_Menu_Click(this, new RoutedEventArgs())); // Ctrl+Alt+N

            // ===== EDIT / SEARCH =====
            Map(Key.F, ModifierKeys.Control, () => Find_Click(this, new RoutedEventArgs()));                     // Ctrl+F
            Map(Key.H, ModifierKeys.Control, () => Replace_Click(this, new RoutedEventArgs()));                  // Ctrl+H
            Map(Key.G, ModifierKeys.Control, () => GoToLine_Click(this, new RoutedEventArgs()));                 // Ctrl+G
            Map(Key.F, ModifierKeys.Control | ModifierKeys.Shift, () => FindInFiles_Click(this, new RoutedEventArgs())); // Ctrl+Shift+F
            Map(Key.I, ModifierKeys.Control | ModifierKeys.Shift, () => FormatDocument_Click(this, new RoutedEventArgs())); // Ctrl+Shift+I
            Map(Key.Oem2, ModifierKeys.Control, ToggleCommentSelection);                                         // Ctrl+/
            Map(Key.D, ModifierKeys.Control, DuplicateLine);                                                     // Ctrl+D
            Map(Key.K, ModifierKeys.Control | ModifierKeys.Shift, DeleteLine);                                   // Ctrl+Shift+K

            // ===== VIEW =====
            Map(Key.B, ModifierKeys.Control, () => ToggleSidebar_Click(this, new RoutedEventArgs()));            // Ctrl+B
            Map(Key.OemPlus, ModifierKeys.Control, () => EditorZoom(+1));                                        // Ctrl+= / +
            Map(Key.Add, ModifierKeys.Control, () => EditorZoom(+1));                                        // Ctrl+Num+
            Map(Key.OemMinus, ModifierKeys.Control, () => EditorZoom(-1));                                        // Ctrl+-
            Map(Key.Subtract, ModifierKeys.Control, () => EditorZoom(-1));                                        // Ctrl+Num-
            Map(Key.D0, ModifierKeys.Control, () => EditorZoom(0));                                              // Ctrl+0
            Map0(Key.F11, ToggleFullscreen);                                                                      // F11

            // ===== NAVIGATE / TABS =====
            Map(Key.Tab, ModifierKeys.Control, NextTab);                                                         // Ctrl+Tab
            Map(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, PreviousTab);                                // Ctrl+Shift+Tab
            Map(Key.D1, ModifierKeys.Control, () => ActivateTab(1));
            Map(Key.D2, ModifierKeys.Control, () => ActivateTab(2));
            Map(Key.D3, ModifierKeys.Control, () => ActivateTab(3));
            Map(Key.D4, ModifierKeys.Control, () => ActivateTab(4));
            Map(Key.D5, ModifierKeys.Control, () => ActivateTab(5));
            Map(Key.D6, ModifierKeys.Control, () => ActivateTab(6));
            Map(Key.D7, ModifierKeys.Control, () => ActivateTab(7));
            Map(Key.D8, ModifierKeys.Control, () => ActivateTab(8));
            Map(Key.D9, ModifierKeys.Control, () => ActivateTab(9));

            // ===== PROJECT / BUILD =====
            Map(Key.B, ModifierKeys.Control | ModifierKeys.Shift, () => Build_Click(this, new RoutedEventArgs())); // Ctrl+Shift+B

            // ===== TOOLS =====
            Map(Key.OemComma, ModifierKeys.Control, () => Settings_Click(this, new RoutedEventArgs()));          // Ctrl+,
            Map(Key.OemComma, ModifierKeys.Control | ModifierKeys.Alt, () => GitAuth_Click(this, new RoutedEventArgs())); // Ctrl+Alt+,
            Map(Key.P, ModifierKeys.Control | ModifierKeys.Shift, () => OpenCommandPalette_Click(this, new RoutedEventArgs())); // Ctrl+Shift+P

            // ===== HELP =====
            Map0(Key.F1, OpenHelpWindow);                                                                         // F1

            // Sanity logs
            if (_completion.LuaHighlight == null && HighlightingManager.Instance.GetDefinition("Lua") == null)
                Log("Lua highlight NOT available — check Resources/Lua.xshd (Build Action: Resource) and XML.");
            else
                Log("Lua highlight is available.");
        }

        private void Log(string text)
        {
            Output.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            Output.ScrollToEnd();
        }

        private void InitializeLogging()
        {
            LogService.OutputSink = msg =>
            {
                if (Dispatcher.CheckAccess())
                {
                    Output.AppendText(msg + Environment.NewLine);
                    Output.ScrollToEnd();
                }
                else
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        Output.AppendText(msg + Environment.NewLine);
                        Output.ScrollToEnd();
                    });
                }
            };
        }

        private bool EnsureProject()
        {
            if (_project == null)
            {
                MessageBox.Show(this, "Open or create a project first.", "No Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            return true;
        }

        // ===== Helpers to find current editor/tab =====
        private bool TryGetActiveEditor(out TextEditor editor, out TabItem tab)
        {
            editor = null!;
            tab = null!;
            if (EditorTabs?.SelectedItem is TabItem ti)
            {
                tab = ti;
                editor = FindEditorIn(ti.Content)!;
                return editor != null;
            }
            return false;
        }

        private TextEditor? FindEditorIn(object? root)
        {
            if (root is TextEditor te) return te;

            if (root is ContentControl cc) return FindEditorIn(cc.Content);

            if (root is System.Windows.Controls.Panel p)
            {
                foreach (UIElement child in p.Children)
                {
                    var found = FindEditorIn(child);
                    if (found != null) return found;
                }
            }

            if (root is System.Windows.Controls.Decorator d) return FindEditorIn(d.Child);

            return null;
        }

        // === Menu click forwarders required by MainWindow.xaml ===
        private void CloseActiveTab_Click(object? sender, RoutedEventArgs e) => CloseActiveTab();
        private void CloseAllTabs_Click(object? sender, RoutedEventArgs e) => CloseAllTabs();
        private void ReopenClosedTab_Click(object? sender, RoutedEventArgs e) => ReopenLastClosedTab();

        private void DuplicateLine_Click(object? sender, RoutedEventArgs e) => DuplicateLine();
        private void DeleteLine_Click(object? sender, RoutedEventArgs e) => DeleteLine();
        private void ToggleComment_Click(object? sender, RoutedEventArgs e) => ToggleCommentSelection();

        private void SaveAs_Click(object? s, RoutedEventArgs e) => SaveAsActiveTab();
        private void ToggleSidebar_Click(object? s, RoutedEventArgs e) => ToggleSidebar();

        // ===== FILE actions used by shortcuts =====
        private void SaveActiveTabShortcut() => Save_Click(this, new RoutedEventArgs());

        private void SaveAsActiveTab()
        {
            if (!TryGetActiveEditor(out var ed, out var tab)) { Status("No editor open"); return; }

            var currentPath = tab.Tag as string ?? "";
            var ext = Path.GetExtension(currentPath);
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save As",
                FileName = Path.GetFileName(currentPath),
                Filter = ext switch
                {
                    ".lua" => "Lua Files (*.lua)|*.lua|All Files|*.*",
                    ".xml" => "XML Files (*.xml)|*.xml|All Files|*.*",
                    ".toc" => "TOC Files (*.toc)|*.toc|All Files|*.*",
                    _ => "All Files|*.*"
                },
                InitialDirectory = Path.GetDirectoryName(currentPath) ?? ""
            };

            if (dlg.ShowDialog(this) == true)
            {
                File.WriteAllText(dlg.FileName, ed.Text, System.Text.Encoding.UTF8);
                tab.Tag = dlg.FileName;
                tab.Header = Path.GetFileName(dlg.FileName);
                FileWatch_NoteJustSaved(dlg.FileName);
                Status($"Saved as {Path.GetFileName(dlg.FileName)}");
            }
        }

        private void CloseActiveTab()
        {
            if (EditorTabs?.SelectedItem is TabItem sel) CloseTab(sel);
        }

        private void CloseAllTabs()
        {
            if (EditorTabs == null) return;
            var items = EditorTabs.Items.OfType<TabItem>().ToArray();
            foreach (var t in items) CloseTab(t);
        }

        private void ReopenLastClosedTab()
        {
            while (_closedTabPaths.Count > 0)
            {
                var path = _closedTabPaths.Pop();
                if (File.Exists(path))
                {
                    OpenFileInTab(path);
                    Status($"Reopened: {Path.GetFileName(path)}");
                    return;
                }
            }
            Status("No closed tabs to reopen.");
        }

        private void ToggleSidebar()
        {
            _sidebarVisible = !_sidebarVisible;
            var col = MainGrid.ColumnDefinitions[0];
            var splitterCol = MainGrid.ColumnDefinitions[1];

            if (_sidebarVisible)
            {
                col.Width = _sidebarSavedWidth;
                splitterCol.Width = new GridLength(8);
                SidebarPanel.Visibility = Visibility.Visible;
                SidebarSplitter.Visibility = Visibility.Visible;
            }
            else
            {
                _sidebarSavedWidth = col.Width;
                col.Width = new GridLength(0);
                splitterCol.Width = new GridLength(0);
                SidebarPanel.Visibility = Visibility.Collapsed;
                SidebarSplitter.Visibility = Visibility.Collapsed;
            }
            Status($"Sidebar: {(_sidebarVisible ? "ON" : "OFF")}");
        }

        // ===== EDITOR operations =====
        private void ToggleCommentSelection()
        {
            if (!TryGetActiveEditor(out var ed, out _)) return;
            var doc = ed.Document; if (doc == null) return;

            // Determine the range we’ll operate on: selected lines or caret line
            ISegment seg = ed.TextArea.Selection.IsEmpty
                ? (ISegment)doc.GetLineByOffset(ed.CaretOffset)
                : ed.TextArea.Selection.SurroundingSegment;

            var startLine = doc.GetLineByOffset(seg.Offset);
            var endLine = doc.GetLineByOffset(seg.EndOffset);

            using (doc.RunUpdate())
            {
                bool allCommented = true;

                for (var line = startLine; line != null && line.Offset <= endLine.Offset; line = line.NextLine)
                {
                    var text = doc.GetText(line);
                    if (!text.TrimStart().StartsWith("--"))
                    {
                        allCommented = false;
                        break;
                    }
                }

                for (var line = startLine; line != null && line.Offset <= endLine.Offset; line = line.NextLine)
                {
                    var text = doc.GetText(line);
                    int leadingSpaces = text.Length - text.TrimStart().Length;

                    if (allCommented)
                    {
                        int idx = text.IndexOf("--", leadingSpaces, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            int removeLen = 2;
                            if (idx + 2 < text.Length && text[idx + 2] == ' ') removeLen++;
                            doc.Remove(line.Offset + idx, removeLen);
                        }
                    }
                    else
                    {
                        doc.Insert(line.Offset + leadingSpaces, "-- ");
                    }
                }
            }
        }

        private void DuplicateLine()
        {
            if (!TryGetActiveEditor(out var ed, out _)) return;
            var doc = ed.Document; if (doc == null) return;

            var line = doc.GetLineByOffset(ed.TextArea.Caret.Offset);
            string text = doc.GetText(line);
            doc.Insert(line.EndOffset, Environment.NewLine + text);
        }

        private void DeleteLine()
        {
            if (!TryGetActiveEditor(out var ed, out _)) return;
            var doc = ed.Document; if (doc == null) return;

            var line = doc.GetLineByOffset(ed.TextArea.Caret.Offset);
            doc.Remove(line.Offset, line.TotalLength);
        }

        private void EditorZoom(int delta)
        {
            if (!TryGetActiveEditor(out var ed, out _)) return;

            if (_defaultEditorFontSize <= 0)
                _defaultEditorFontSize = ed.FontSize;

            if (delta == 0)
            {
                ed.FontSize = _defaultEditorFontSize;
                Status("Zoom reset");
                return;
            }

            ed.FontSize = Math.Clamp(ed.FontSize + delta, 8, 40);
            Status($"Zoom {(delta > 0 ? "in" : "out")} → {ed.FontSize:0}");
        }

        // ===== Tabs / navigation =====
        private void NextTab()
        {
            if (EditorTabs is null || EditorTabs.Items.Count == 0) return;
            EditorTabs.SelectedIndex = (EditorTabs.SelectedIndex + 1) % EditorTabs.Items.Count;
        }

        private void PreviousTab()
        {
            if (EditorTabs is null || EditorTabs.Items.Count == 0) return;
            EditorTabs.SelectedIndex = (EditorTabs.SelectedIndex - 1 + EditorTabs.Items.Count) % EditorTabs.Items.Count;
        }

        private void ActivateTab(int oneBased)
        {
            if (EditorTabs is null) return;
            var idx = Math.Clamp(oneBased - 1, 0, EditorTabs.Items.Count - 1);
            EditorTabs.SelectedIndex = idx;
        }

        private void ToggleFullscreen()
        {
            if (WindowStyle == WindowStyle.None)
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
        }

        // ===== New File / New Folder feature =====

        private void NewFile_Menu_Click(object? sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            var baseDir = _project!.RootPath; // default: project root
            CreateNewFileFlow(baseDir);
        }

        private void NewFile_Context_Click(object? sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;

            var baseDir = _project!.RootPath;
            try
            {
                var sel = ProjectTree?.SelectedItem;
                var selectedPath = GetPathForTreeItem(sel);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    baseDir = Directory.Exists(selectedPath) ? selectedPath : Path.GetDirectoryName(selectedPath)!;
                }
            }
            catch (Exception ex) { LogService.Warn("NewFile_Context_Click: failed to resolve selected path", ex); }

            CreateNewFileFlow(baseDir);
        }

        private void NewFolder_Context_Click(object? sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;

            var baseDir = _project!.RootPath;
            try
            {
                var sel = ProjectTree?.SelectedItem;
                var selectedPath = GetPathForTreeItem(sel);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    baseDir = Directory.Exists(selectedPath) ? selectedPath : Path.GetDirectoryName(selectedPath)!;
                }
            }
            catch (Exception ex) { LogService.Warn("NewFolder_Context_Click: failed to resolve selected path", ex); }

            var name = Microsoft.VisualBasic.Interaction.InputBox(
                $"New folder name under:\n{MakeRelativeToProject(baseDir)}",
                "New Folder", "NewFolder");

            if (string.IsNullOrWhiteSpace(name)) return;

            if (!IsValidSimpleName(name)) { MessageBox.Show(this, "Invalid folder name.", "New Folder", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var target = Path.Combine(baseDir, name);
            if (!IsPathInsideProject(target)) { MessageBox.Show(this, "Folder must be inside the project.", "New Folder", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            Directory.CreateDirectory(target);
            RefreshProjectExplorer();
            SelectPathInProjectTree(target);
            Status($"Folder created: {MakeRelativeToProject(target)}");
        }

        private void CreateNewFileFlow(string baseDir)
        {
            var prompt = $"Enter file name (relative to {MakeRelativeToProject(baseDir)}):\nExamples: MyAddon.lua, Scripts/Main.lua, MyAddon.toc";
            var input = Microsoft.VisualBasic.Interaction.InputBox(prompt, "New File", "NewFile.lua");
            if (string.IsNullOrWhiteSpace(input)) return;

            // Normalize + prevent rooted/escape
            input = input.Replace('\\', '/').Trim().TrimStart('/');
            if (input.IndexOfAny(Path.GetInvalidPathChars()) >= 0) { MessageBox.Show(this, "Invalid path characters.", "New File", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var fullPath = Path.GetFullPath(Path.Combine(baseDir, input));
            if (!IsPathInsideProject(fullPath))
            {
                MessageBox.Show(this, "The file must be inside the project folder.", "New File", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dir = Path.GetDirectoryName(fullPath)!;
            Directory.CreateDirectory(dir);

            if (File.Exists(fullPath))
            {
                MessageBox.Show(this, "A file with that name already exists.", "New File", MessageBoxButton.OK, MessageBoxImage.Information);
                OpenFileInEditor(fullPath);
                SelectPathInProjectTree(fullPath);
                return;
            }

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var contents = NewFileTemplate(ext);

            File.WriteAllText(fullPath, contents ?? string.Empty);
            RefreshProjectExplorer();
            SelectPathInProjectTree(fullPath);
            OpenFileInEditor(fullPath);
            Status($"File created: {MakeRelativeToProject(fullPath)}");
        }

        private string? NewFileTemplate(string ext)
        {
            var addonName = _project?.Name ?? "MyAddon";
            var date = DateTime.Now.ToString("yyyy-MM-dd");

            return ext switch
            {
                ".lua" => $"-- {addonName}\n-- Created: {date}\n\nlocal addonName, ns = ...\n\n",
                ".xml" => $"<!-- {addonName} - Created {date} -->\n<Ui xmlns=\"http://www.blizzard.com/wow/ui/\">\n    <!-- Add frames here -->\n</Ui>\n",
                ".toc" => $"## Interface: 110000\n## Title: {addonName}\n## Notes: {addonName}\n## Author: You\n## Version: 0.1.0\n\nmain.lua\n",
                _ => string.Empty
            };
        }

        // ===== Helpers used by New File/Folder =====

        private string GetPathForTreeItem(object? node)
        {
            if (node is string s) return s;
            if (node is FileInfo fi) return fi.FullName;
            if (node is DirectoryInfo di) return di.FullName;

            var type = node?.GetType();
            var prop = type?.GetProperty("FullPath") ?? type?.GetProperty("Path");
            if (prop?.GetValue(node) is string p && !string.IsNullOrWhiteSpace(p)) return p;

            return _project?.RootPath ?? "";
        }

        private bool IsPathInsideProject(string absolutePath)
        {
            if (_project == null) return false;
            var proj = Path.GetFullPath(_project.RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var abs = Path.GetFullPath(absolutePath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return abs.StartsWith(proj, StringComparison.OrdinalIgnoreCase);
        }

        private string MakeRelativeToProject(string absolutePath)
        {
            if (_project == null) return absolutePath;
            var proj = Path.GetFullPath(_project.RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var abs = Path.GetFullPath(absolutePath);
            if (abs.StartsWith(proj, StringComparison.OrdinalIgnoreCase))
                return abs.Substring(proj.Length);
            return absolutePath;
        }

        private bool IsValidSimpleName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !name.Contains('/') && !name.Contains('\\');
        }

        private void RefreshProjectExplorer()
        {
            if (_project == null || string.IsNullOrWhiteSpace(_project.RootPath) || !Directory.Exists(_project.RootPath))
                return;

            // Capture selection + expansion state
            string? selectedPath = null;
            if (ProjectTree?.SelectedItem != null)
            {
                selectedPath =
                    (ProjectTree.SelectedItem as TreeViewItem)?.Tag as string
                    ?? GetPathForTreeItem(ProjectTree.SelectedItem);
            }

            if (ProjectTree == null) return;

            var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CaptureExpanded(ProjectTree.Items, expanded);

            // Rebuild
            ProjectTree.Items.Clear();

            var rootDir = new DirectoryInfo(_project.RootPath);
            var rootNode = CreateDirNode(rootDir);
            ProjectTree.Items.Add(rootNode);
            BuildChildren(rootNode, rootDir);

            // Restore expansion state
            RestoreExpanded(ProjectTree.Items, expanded);

            // Restore selection
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                var item = FindItemByPath(ProjectTree.Items, selectedPath);
                if (item != null)
                {
                    item.IsSelected = true;
                    item.BringIntoView();
                }
            }
        }

        private void CaptureExpanded(ItemCollection items, HashSet<string> expanded)
        {
            foreach (var obj in items)
            {
                if (obj is TreeViewItem tvi)
                {
                    if (tvi.IsExpanded && tvi.Tag is string path)
                        expanded.Add(path);

                    CaptureExpanded(tvi.Items, expanded);
                }
            }
        }

        private void RestoreExpanded(ItemCollection items, HashSet<string> expanded)
        {
            foreach (var obj in items)
            {
                if (obj is TreeViewItem tvi && tvi.Tag is string path)
                {
                    if (expanded.Contains(path))
                        tvi.IsExpanded = true;

                    RestoreExpanded(tvi.Items, expanded);
                }
            }
        }

        private TreeViewItem? FindItemByPath(ItemCollection items, string path)
        {
            foreach (var obj in items)
            {
                if (obj is TreeViewItem tvi)
                {
                    if (string.Equals(tvi.Tag as string, path, StringComparison.OrdinalIgnoreCase))
                        return tvi;

                    var deeper = FindItemByPath(tvi.Items, path);
                    if (deeper != null) return deeper;
                }
            }
            return null;
        }

        private void BuildChildren(TreeViewItem dirItem, DirectoryInfo dir)
        {
            // Folders first
            DirectoryInfo[] subDirs = Array.Empty<DirectoryInfo>();
            FileInfo[] files = Array.Empty<FileInfo>();

            try { subDirs = dir.GetDirectories(); } catch (Exception ex) { LogService.Error("BuildChildren: failed to enumerate subdirectories", ex); }
            try { files = dir.GetFiles(); } catch (Exception ex) { LogService.Error("BuildChildren: failed to enumerate files", ex); }

            foreach (var d in subDirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            {
                var child = CreateDirNode(d);
                dirItem.Items.Add(child);
                BuildChildren(child, d);
            }

            foreach (var f in files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            {
                dirItem.Items.Add(CreateFileNode(f));
            }
        }

        private TreeViewItem CreateDirNode(DirectoryInfo di)
        {
            return new TreeViewItem
            {
                Header = MakeHeader("📁", di.Name),
                Tag = di.FullName,
                DataContext = di, // lets GetPathForTreeItem see DirectoryInfo if you use it
                IsExpanded = false
            };
        }

        private TreeViewItem CreateFileNode(FileInfo fi)
        {
            string emoji = EmojiForExtension(fi.Extension);
            return new TreeViewItem
            {
                Header = MakeHeader(emoji, fi.Name),
                Tag = fi.FullName,
                DataContext = fi
            };
        }

        private object MakeHeader(string emoji, string text)
        {
            var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = emoji, FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"), Margin = new Thickness(0, 0, 6, 0) });
            sp.Children.Add(new TextBlock { Text = text });
            return sp;
        }

        private string EmojiForExtension(string ext)
        {
            ext = (ext ?? "").ToLowerInvariant();
            return ext switch
            {
                ".lua" => "🐍",  // close-enough programmer snake (or choose 📜)
                ".xml" => "🧩",
                ".toc" => "📑",
                ".md" => "📝",
                ".txt" => "📄",
                ".png" or ".jpg" or ".jpeg" or ".gif" => "🖼️",
                ".json" => "🧾",
                _ => "📄"
            };
        }

        private void SelectPathInProjectTree(string fullPath)
        {
            try
            {
                // Optional: programmatic selection if your tree supports it.
            }
            catch (Exception ex) { LogService.Warn("SelectPathInProjectTree: failed to select path", ex); }
        }

        private void OpenFileInEditor(string fullPath)
        {
            try
            {
                // If you already have a real opener, call it here.
                OpenPathFallback(fullPath);
            }
            catch (Exception ex)
            {
                LogService.Warn("OpenFileInEditor: couldn't auto-open file", ex);
                Status("Created file (couldn't auto-open).");
            }
        }

        // Single status helper (ensure there is ONLY ONE across all partials)
        private void Status(string text) => StatusText.Text = text;


        private void OpenPathFallback(string path)
        {
            if (!File.Exists(path)) return;

            var editor = new TextEditor
            {
                ShowLineNumbers = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 14,
                SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(path))
            };
            editor.Text = File.ReadAllText(path);

            var tab = new TabItem { Header = System.IO.Path.GetFileName(path), Content = editor, Tag = path };
            EditorTabs.Items.Add(tab);
            EditorTabs.SelectedItem = tab;

            ThemeManager.ApplyToEditor(editor);
        }

        // ===== Minimal RelayCommand (window-scoped) =====
        public class RelayCommand : ICommand
        {
            private readonly Action<object?> _exec; private readonly Func<object?, bool>? _can;
            public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null) { _exec = exec; _can = can; }
            public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
            public void Execute(object? p) => _exec(p);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }
    }
}