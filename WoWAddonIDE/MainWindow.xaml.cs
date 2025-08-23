using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;

using Microsoft.VisualBasic; // remove if you replace InputBox with your own dialog
using Newtonsoft.Json;
using Ookii.Dialogs.Wpf;

using WoWAddonIDE.Models;
using WoWAddonIDE.Services;
using WoWAddonIDE.Windows;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private AddonProject? _project;
        private readonly string _settingsPath;
        private IDESettings _settings;

        private CompletionService _completion;

        public MainWindow()
        {
            InitializeComponent();

            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WoWAddonIDE", "settings.json");

            _settings = LoadSettings();

            // Initialize completion/highlighting (loads wow_api.json; may also load Lua.xshd)
            _completion = new CompletionService();

            // Also run a robust fallback/registration pass for Lua highlighting
            EnsureLuaHighlightRegistered();

            Status("Ready");
            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath))
            {
                _settings.AddOnsPath = DetectAddOnsPath();
                SaveSettings();
            }

            PathText.Text = string.IsNullOrWhiteSpace(_settings.AddOnsPath)
                ? "AddOns Path: (not set) — Tools > Settings..."
                : $"AddOns Path: {_settings.AddOnsPath}";

            // Sanity logs so we can diagnose quickly
            if (_completion.LuaHighlight == null && HighlightingManager.Instance.GetDefinition("Lua") == null)
                Log("Lua highlight NOT available — check Resources/Lua.xshd (Build Action: Resource) and XML.");
            else
                Log("Lua highlight is available.");

            // Global keybinding: Find in Files
            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => FindInFiles_Click(this, new RoutedEventArgs())),
                new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Shift)));
        }

        // -------- Robust Lua XSHD loader: probes resource, pack URI, disk, and manifest; registers with HighlightingManager
        private void EnsureLuaHighlightRegistered()
        {
            var existing = HighlightingManager.Instance.GetDefinition("Lua");
            if (existing != null)
            {
                Log("Lua highlight already registered.");
                return;
            }

            Stream? TryOpen()
            {
                // 1) WPF relative resource
                try
                {
                    var s = Application.GetResourceStream(new Uri("Resources/Lua.xshd", UriKind.Relative))?.Stream;
                    Log(s != null ? "Lua.xshd found via relative resource." : "Relative resource NOT found.");
                    if (s != null) return s;
                }
                catch (Exception ex)
                {
                    Log($"Relative resource error: {ex.Message}");
                }

                // 2) Pack URI (auto-detect assembly name)
                try
                {
                    var asmName = Assembly.GetExecutingAssembly().GetName().Name ?? "WoWAddonIDE";
                    var s = Application.GetResourceStream(new Uri($"/{asmName};component/Resources/Lua.xshd", UriKind.Relative))?.Stream;
                    Log(s != null ? "Lua.xshd found via pack URI." : "Pack URI NOT found.");
                    if (s != null) return s;
                }
                catch (Exception ex)
                {
                    Log($"Pack URI error: {ex.Message}");
                }

                // 3) Disk: bin\Resources\Lua.xshd or bin\Lua.xshd
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var p1 = Path.Combine(baseDir, "Resources", "Lua.xshd");
                    var p2 = Path.Combine(baseDir, "Lua.xshd");

                    if (File.Exists(p1))
                    {
                        Log($"Lua.xshd found on disk: {p1}");
                        return File.OpenRead(p1);
                    }
                    if (File.Exists(p2))
                    {
                        Log($"Lua.xshd found on disk: {p2}");
                        return File.OpenRead(p2);
                    }
                    Log("Disk probe NOT found (bin/Resources or bin root).");
                }
                catch (Exception ex)
                {
                    Log($"Disk probe error: {ex.Message}");
                }

                // 4) Embedded manifest (if incorrectly added as EmbeddedResource)
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    var resName = asm.GetManifestResourceNames()
                                     .FirstOrDefault(n => n.EndsWith("Lua.xshd", StringComparison.OrdinalIgnoreCase));
                    if (resName != null)
                    {
                        Log($"Lua.xshd found as manifest resource: {resName}");
                        return asm.GetManifestResourceStream(resName);
                    }
                    Log("No manifest resource named *Lua.xshd* found.");
                }
                catch (Exception ex)
                {
                    Log($"Manifest probe error: {ex.Message}");
                }

                return null;
            }

            try
            {
                using var stream = TryOpen();
                if (stream == null)
                {
                    Log("Lua.xshd still not found after all probes.");
                    return;
                }

                using var reader = new XmlTextReader(stream);
                var def = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting("Lua", new[] { ".lua" }, def);
                Log("Lua highlighting registered successfully.");
            }
            catch (Exception ex)
            {
                Log($"Failed to load/register Lua highlighting: {ex.Message}");
            }
        }

        // ========================= File Menu =========================

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            var folderDlg = new VistaFolderBrowserDialog
            {
                Description = "Choose a workspace folder to create your addon project inside."
            };
            if (folderDlg.ShowDialog(this) != true) return;

            var addonName = Interaction.InputBox("Addon name:", "New Addon", "MyAddon");
            if (string.IsNullOrWhiteSpace(addonName)) return;

            try
            {
                var projRoot = Path.Combine(folderDlg.SelectedPath, addonName);
                Directory.CreateDirectory(projRoot);

                var toc = Path.Combine(projRoot, $"{addonName}.toc");
                File.WriteAllText(toc, TocParser.GenerateDefaultToc(addonName, "110005"));

                var mainLua = Path.Combine(projRoot, "Main.lua");
                File.WriteAllText(mainLua,
@"-- Main entry
local ADDON_NAME, ns = ...
print(ADDON_NAME .. ' loaded!')
");

                _project = AddonProject.LoadFromDirectory(projRoot);
                RefreshProjectTree();
                OpenFileInTab(mainLua);

                Status($"Created project: {addonName}");
                PathText.Text = $"Project: {_project.RootPath}";
                Log($"New project created at {projRoot}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "New Project Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e)
        {
            var folderDlg = new VistaFolderBrowserDialog
            {
                Description = "Select your addon project folder (where the .toc is)."
            };
            if (folderDlg.ShowDialog(this) != true) return;

            try
            {
                _project = AddonProject.LoadFromDirectory(folderDlg.SelectedPath);
                if (_project == null)
                {
                    MessageBox.Show(this, "No .toc found in that directory.", "Open Project", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                RefreshProjectTree();
                Status($"Opened project: {_project.Name}");
                PathText.Text = $"Project: {_project.RootPath}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open Project Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) => SaveActiveTab();

        private void SaveAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (TabItem tab in EditorTabs.Items) SaveTab(tab);
            Status("Saved all files");
        }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        // ======================== Project Menu =======================

        private void AddLua_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            var name = Interaction.InputBox("New Lua filename (without path):", "Add Lua File", "NewFile.lua");
            if (string.IsNullOrWhiteSpace(name)) return;
            var full = Path.Combine(_project!.RootPath, name);
            if (!full.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) full += ".lua";
            File.WriteAllText(full, "-- New Lua file\n");
            _project = AddonProject.LoadFromDirectory(_project.RootPath);
            RefreshProjectTree();
            OpenFileInTab(full);
            TryAddToToc(full);
        }

        private void AddXml_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            var name = Interaction.InputBox("New XML filename (without path):", "Add XML File", "Frame.xml");
            if (string.IsNullOrWhiteSpace(name)) return;
            var full = Path.Combine(_project!.RootPath, name);
            if (!full.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) full += ".xml";
            File.WriteAllText(full,
@"<Ui xmlns=""http://www.blizzard.com/wow/ui/"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
    <!-- Add your frames here -->
</Ui>");
            _project = AddonProject.LoadFromDirectory(_project.RootPath);
            RefreshProjectTree();
            OpenFileInTab(full);
            TryAddToToc(full);
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            SaveAll_Click(sender, e);

            var problems = LuaLint.Pass(_project!);
            foreach (var p in problems) Log(p);

            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set or invalid. Go to Tools > Settings.", "Build", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var src = Path.GetFullPath(_project!.RootPath);
                var addons = Path.GetFullPath(_settings.AddOnsPath);
                var target = Path.GetFullPath(Path.Combine(addons, _project!.Name));

                // Safety rails:
                // 1) target must be inside AddOns (not equal to AddOns)
                // 2) never allow deleting if source == target
                // 3) warn if project already lives under AddOns (dev edits directly there)
                bool sourceInsideAddons = src.StartsWith(addons, StringComparison.OrdinalIgnoreCase);
                if (sourceInsideAddons)
                {
                    Log("Project is already inside AddOns; copying is unnecessary.");
                    MessageBox.Show(this,
                        "Your project folder is already inside the AddOns directory.\n" +
                        "Build will NOT delete or copy anything to avoid data loss.",
                        "Build Skipped", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Ensure target is a subfolder of AddOns
                if (!target.StartsWith(addons, StringComparison.OrdinalIgnoreCase) || string.Equals(target, addons, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "Calculated output path is invalid. Check Tools > Settings.", "Build Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // If target exists, wipe it *only if* it is different from src
                if (Directory.Exists(target))
                {
                    if (string.Equals(src, target, StringComparison.OrdinalIgnoreCase))
                    {
                        // should never happen with the check above — but keep belt & suspenders
                        MessageBox.Show(this, "Source and target are the same folder. Aborting.", "Build Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    Directory.Delete(target, true);
                }

                Directory.CreateDirectory(target);

                // Copy all files (preserve subdirs). Skip hidden/temp files.
                foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(src, file);
                    var dest = Path.Combine(target, rel);

                    // Skip common junk
                    var fi = new FileInfo(file);
                    if ((fi.Attributes & FileAttributes.Hidden) != 0) continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest, true);
                }

                Status("Build succeeded (copied to AddOns).");
                Log($"Build copied to: {target}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenAddOnsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set or invalid. Go to Tools > Settings.", "Open AddOns Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            System.Diagnostics.Process.Start("explorer.exe", _settings.AddOnsPath);
        }

        private void Clean_Click(object sender, RoutedEventArgs e) => Output.Clear();

        // ========================= Tools Menu ========================

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new VistaFolderBrowserDialog
            {
                Description = "Select your World of Warcraft _retail_\\Interface\\AddOns folder."
            };
            if (dlg.ShowDialog(this) == true)
            {
                _settings.AddOnsPath = dlg.SelectedPath;
                SaveSettings();
                PathText.Text = $"AddOns Path: {_settings.AddOnsPath}";
                Log($"AddOns path set to: {_settings.AddOnsPath}");
            }
        }

        private void GenerateToc_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            var tocPath = _project!.TocPath;
            var content = TocParser.GenerateDefaultToc(_project.Name, _project.InterfaceVersion ?? "110005");
            foreach (var f in _project.Files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    var rel = Path.GetRelativePath(_project.RootPath, f).Replace("\\", "/");
                    content += rel + Environment.NewLine;
                }
            }
            File.WriteAllText(tocPath, content);
            Log("Regenerated .toc");
            OpenFileInTab(tocPath);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this,
                "WoW Addon IDE (WPF)\n\n" +
                "• Lua & XML syntax highlighting\n" +
                "• Autocomplete + parameter hints\n" +
                "• Project explorer + Build to AddOns\n" +
                "• TOC generator\n" +
                "• Find in Files (Ctrl+Shift+F)\n" +
                "• Lua Outline panel\n" +
                "• Toggle comment (Ctrl+/), Duplicate line (Ctrl+D)\n",
                "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ====================== Project Tree / Tabs ======================

        private void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ProjectTree.SelectedItem is TreeViewItem tvi && tvi.Tag is string path && File.Exists(path))
            {
                OpenFileInTab(path);
            }
        }

        private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshOutlineForActive();
        }

        private void RefreshProjectTree()
        {
            ProjectTree.Items.Clear();
            if (_project == null) return;

            var rootItem = new TreeViewItem { Header = _project.Name, Tag = _project.RootPath, IsExpanded = true };

            // .toc on top if present
            if (File.Exists(_project.TocPath))
            {
                rootItem.Items.Add(new TreeViewItem
                {
                    Header = System.IO.Path.GetFileName(_project.TocPath),
                    Tag = _project.TocPath
                });
            }

            // Build a compact tree from the project root
            void AddDir(TreeViewItem parent, string dir)
            {
                // Folders
                foreach (var sub in Directory.GetDirectories(dir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    var node = new TreeViewItem
                    {
                        Header = Path.GetFileName(sub),
                        Tag = sub,
                        IsExpanded = false
                    };
                    AddDir(node, sub);
                    parent.Items.Add(node);
                }

                // Files
                foreach (var file in Directory.GetFiles(dir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    // hide .tga/.png unless you want them
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext is ".tga" or ".png" or ".jpg") continue;

                    parent.Items.Add(new TreeViewItem
                    {
                        Header = Path.GetFileName(file),
                        Tag = file
                    });
                }
            }

            AddDir(rootItem, _project.RootPath);
            rootItem.IsExpanded = true;

            ProjectTree.Items.Add(rootItem);
        }

        // ============================ Editors ============================

        private void OpenFileInTab(string path)
        {
            foreach (TabItem tab in EditorTabs.Items)
            {
                if ((tab.Tag as string) == path)
                {
                    EditorTabs.SelectedItem = tab;
                    return;
                }
            }

            var editor = new TextEditor
            {
                ShowLineNumbers = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            // QoL
            editor.Options.HighlightCurrentLine = true;
            editor.Options.IndentationSize = 4;
            editor.Options.ConvertTabsToSpaces = true;

            // Syntax highlighting (Lua via CompletionService or registered name; XML via built-in)
            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                editor.SyntaxHighlighting = _completion.LuaHighlight
                    ?? HighlightingManager.Instance.GetDefinition("Lua");
            }
            else if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
            }

            // Load file
            editor.Text = File.ReadAllText(path);

            // Completion + parameter hints
            editor.TextArea.TextEntered += (s, e) =>
            {
                if (e.Text.Length == 1)
                {
                    var ch = e.Text[0];

                    if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '!' || ch == '.')
                    {
                        var word = CompletionService.GetCurrentWord(editor.TextArea);
                        if (word.Length >= 1)
                            _completion.ShowCompletion(editor.TextArea, word);
                    }

                    if (ch == '(')
                    {
                        var fname = CompletionService.GetWordBeforeChar(editor.TextArea, '(');
                        if (!string.IsNullOrWhiteSpace(fname))
                            _completion.ShowParameterHints(editor.TextArea, fname);
                    }
                }
            };

            editor.TextChanged += (s, e) =>
            {
                MarkTabDirty(path, true);
                if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                    RefreshOutlineForActive();
            };

            // Toggle comment (Ctrl+/)
            editor.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => ToggleComment(editor)),
                new KeyGesture(Key.Oem2, ModifierKeys.Control))); // '/'

            // Duplicate line (Ctrl+D)
            editor.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => DuplicateLine(editor)),
                new KeyGesture(Key.D, ModifierKeys.Control)));

            var tabItem = new TabItem
            {
                Header = System.IO.Path.GetFileName(path),
                Content = editor,
                Tag = path
            };

            EditorTabs.Items.Add(tabItem);
            EditorTabs.SelectedItem = tabItem;

            // Update outline for this file
            RefreshOutlineForActive();
        }

        private void SaveActiveTab()
        {
            if (EditorTabs.SelectedItem is TabItem tab) SaveTab(tab);
        }

        private void SaveTab(TabItem tab)
        {
            var path = tab.Tag as string;
            if (string.IsNullOrWhiteSpace(path)) return;
            if (tab.Content is TextEditor editor)
            {
                File.WriteAllText(path, editor.Text);
                MarkTabDirty(path, false);
                Status($"Saved {System.IO.Path.GetFileName(path)}");
                if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                    RefreshOutlineForActive();
            }
        }

        private void MarkTabDirty(string path, bool dirty)
        {
            foreach (TabItem tab in EditorTabs.Items)
            {
                if ((tab.Tag as string) == path)
                {
                    var name = System.IO.Path.GetFileName(path);
                    tab.Header = dirty ? name + "*" : name;
                    return;
                }
            }
        }

        private void TryAddToToc(string filePath)
        {
            if (_project == null) return;
            var rel = Path.GetRelativePath(_project.RootPath, filePath).Replace("\\", "/");
            var lines = File.ReadAllLines(_project.TocPath).ToList();
            if (!lines.Any(l => l.Trim().Equals(rel, StringComparison.OrdinalIgnoreCase)))
            {
                lines.Add(rel);
                File.WriteAllLines(_project.TocPath, lines);
                Log($"Added to TOC: {rel}");
            }
        }

        // ====================== Outline helpers ======================

        private void RefreshOutlineForActive()
        {
            if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed && (tab.Tag as string) is string path)
            {
                if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    var items = OutlineService.Build(ed.Text);
                    Outline.ItemsSource = items;
                    return;
                }
            }
            Outline.ItemsSource = null;
        }

        private void Outline_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Outline.SelectedItem is OutlineService.OutlineItem it)
            {
                if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed)
                {
                    var off = GetOffsetForLine(ed.Text, it.Line);
                    ed.SelectionStart = off;
                    ed.SelectionLength = 0;
                    ed.ScrollToLine(it.Line);
                    ed.Focus();
                }
            }
        }

        private static int GetOffsetForLine(string text, int line)
        {
            int current = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (current == line) return i;
                if (text[i] == '\n') current++;
            }
            return text.Length;
        }

        // ====================== Find in Files ======================

        private void FindInFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) { MessageBox.Show(this, "Open or create a project first."); return; }
            var dlg = new FindInFilesWindow { Owner = this, ProjectRoot = _project.RootPath };
            dlg.NavigateTo += (file, line, col) =>
            {
                OpenFileInTab(file);
                if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed)
                {
                    ed.ScrollToLine(line);
                    ed.CaretOffset = Math.Min(ed.Document.TextLength, ed.Document.GetOffset(line, Math.Max(col, 1)));
                    ed.Focus();
                }
            };
            dlg.ShowDialog();
        }

        // ====================== Settings & Helpers ======================

        private IDESettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    return JsonConvert.DeserializeObject<IDESettings>(json) ?? new IDESettings();
                }
            }
            catch { /* ignore */ }
            return new IDESettings();
        }

        private void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_settingsPath,
                    JsonConvert.SerializeObject(_settings, Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log($"Failed to save settings: {ex.Message}");
            }
        }

        private static string DetectAddOnsPath()
        {
            var candidates = new[]
            {
                @"C:\Program Files (x86)\World of Warcraft\_retail_\Interface\AddOns",
                @"C:\Program Files\World of Warcraft\_retail_\Interface\AddOns",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"World of Warcraft","_retail_","Interface","AddOns"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"World of Warcraft","_retail_","Interface","AddOns")
            };
            return candidates.FirstOrDefault(Directory.Exists) ?? "";
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

        private void Log(string text)
        {
            Output.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            Output.ScrollToEnd();
        }

        private void Status(string text) => StatusText.Text = text;
    }

    // Keep simple settings POCO here for convenience
    public class IDESettings
    {
        public string AddOnsPath { get; set; } = "";
    }

    // Small command helper for keybindings
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _exec; private readonly Func<object?, bool>? _can;
        public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null) { _exec = exec; _can = can; }
        public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
        public void Execute(object? p) => _exec(p);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }

    // Editor helpers
    public partial class MainWindow
    {
        private void ToggleComment(TextEditor ed)
        {
            var doc = ed.Document;
            var selStart = ed.SelectionStart;
            var selEnd = ed.SelectionStart + ed.SelectionLength;
            var startLine = doc.GetLineByOffset(selStart);
            var endLine = doc.GetLineByOffset(selEnd);

            using (doc.RunUpdate())
            {
                var line = startLine;
                while (true)
                {
                    var text = doc.GetText(line.Offset, line.Length);
                    var trimmed = text.TrimStart();
                    var leading = text.Length - trimmed.Length;
                    if (trimmed.StartsWith("--"))
                    {
                        // remove first "--" after leading whitespace
                        var idxInLine = leading + trimmed.IndexOf("--", StringComparison.Ordinal);
                        doc.Remove(line.Offset + idxInLine, 2);
                    }
                    else
                    {
                        // insert at start of line
                        doc.Insert(line.Offset + leading, "--");
                    }

                    if (line == endLine) break;
                    line = line.NextLine!;
                }
            }
        }

        private void ProjectTree_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ProjectTree.SelectedItem is TreeViewItem tvi && tvi.Tag is string path && File.Exists(path))
            {
                OpenFileInTab(path);
            }
        }

        private void DuplicateLine(TextEditor ed)
        {
            var doc = ed.Document;
            var line = doc.GetLineByOffset(ed.CaretOffset);
            var text = doc.GetText(line.Offset, line.TotalLength);
            doc.Insert(line.EndOffset, text);
        }
    }
}