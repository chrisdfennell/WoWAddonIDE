using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using LibGit2Sharp;
using Microsoft.VisualBasic; // used for Interaction.InputBox
using Newtonsoft.Json;
using Octokit;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Xml;
using System.Xml.Linq;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;
using WoWAddonIDE.Windows;
using Microsoft.Win32;
using System.Net.Http;
using Application = System.Windows.Application;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private AddonProject? _project;
        private readonly string _settingsPath;
        private IDESettings _settings;

        // Editor toggles (session-scoped)
        private bool _wordWrap = false;
        private bool _showInvisibles = false;

        private System.Windows.Threading.DispatcherTimer? _gitStatusTimer;

        private CompletionService _completion;

        // Hover docs (from Resources/wow_api.json)
        private Dictionary<string, ApiEntry> _apiDocs = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, List<SymbolService.SymbolLocation>> _symbolIndex = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _symbolIndexBuilt = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WoWAddonIDE", "settings.json");

            _settings = LoadSettings();

            // Initialize completion/highlighting (loads wow_api.json for completion)
            _completion = new CompletionService();

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

            // Sanity logs
            if (_completion.LuaHighlight == null && HighlightingManager.Instance.GetDefinition("Lua") == null)
                Log("Lua highlight NOT available — check Resources/Lua.xshd (Build Action: Resource) and XML.");
            else
                Log("Lua highlight is available.");

            // Global keybinding: Find in Files
            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => FindInFiles_Click(this, new RoutedEventArgs())),
                new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Shift)));

            // THEME: initialize + apply + listen for changes
            ThemeManager.Initialize(_settings);
            ThemeManager.Persist = SaveSettings;
            ThemeManager.ApplyTheme(_settings.ThemeMode); // System/Light/Dark

            ThemeManager.ThemeChanged += () =>
            {
                foreach (TabItem tab in EditorTabs.Items)
                    if (tab.Content is TextEditor ed)
                    {
                        ThemeManager.ApplyToEditor(ed);
                        if (ed.SyntaxHighlighting != null)
                            RetintHighlighting(ed.SyntaxHighlighting, IsDarkThemeActive());
                    }
            };
        }

        // Returns true if current theme is dark (after considering System mode)
        private bool IsDarkThemeActive()
        {
            var mode = _settings.ThemeMode == ThemeMode.System
                ? ThemeManager.GetOsTheme()
                : _settings.ThemeMode;
            return mode == ThemeMode.Dark;
        }

        private void UpdateGitStatusStrip()
        {
            if (_project == null) { GitBranchText.Text = ""; return; }
            try
            {
                var info = GitService.GetRepoInfo(_project.RootPath);
                GitBranchText.Text =
                    $"{info.Branch ?? "(no branch)"}  ·  +{info.Ahead}/-{info.Behind}  " +
                    $"Δ {info.Added} / −{info.Deleted}  " +
                    (info.Conflicts > 0 ? $"⚠ {info.Conflicts} conflicts" : "");
            }
            catch { GitBranchText.Text = ""; }
        }


        private void GitBlameActive_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) { MessageBox.Show(this, "Open a project first."); return; }
            if (EditorTabs.SelectedItem is not TabItem tab || tab.Tag is not string path) { MessageBox.Show(this, "Open a file tab."); return; }
            if (!File.Exists(path)) { MessageBox.Show(this, "File not found on disk."); return; }

            var w = new WoWAddonIDE.Windows.BlameWindow { Owner = this };
            w.LoadBlame(_project.RootPath, path);
            w.ShowDialog();
        }

        private void GitHistoryActive_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) { MessageBox.Show(this, "Open a project first."); return; }
            if (EditorTabs.SelectedItem is not TabItem tab || tab.Tag is not string path) { MessageBox.Show(this, "Open a file tab."); return; }
            if (!File.Exists(path)) { MessageBox.Show(this, "File not found on disk."); return; }

            var w = new WoWAddonIDE.Windows.HistoryWindow { Owner = this };
            w.LoadHistory(_project.RootPath, path);
            w.ShowDialog();
        }

        private void GitMergeHelper_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) { MessageBox.Show(this, "Open a project first."); return; }

            var repoRoot = GitService.FindRepoRoot(_project.RootPath) ?? _project.RootPath;
            var w = new WoWAddonIDE.Windows.MergeHelperWindow(repoRoot) { Owner = this };

            w.LoadConflicts();   // parameterless
            w.ShowDialog();

            UpdateGitStatusStrip();
        }

        private ICSharpCode.AvalonEdit.TextEditor? ActiveEditor()
        {
            if (EditorTabs.SelectedItem is TabItem tab && tab.Content is ICSharpCode.AvalonEdit.TextEditor ed)
                return ed;
            return null;
        }

        private IEnumerable<ICSharpCode.AvalonEdit.TextEditor> AllEditors()
        {
            foreach (var obj in EditorTabs.Items)
                if (obj is TabItem t && t.Content is ICSharpCode.AvalonEdit.TextEditor ed)
                    yield return ed;
        }

        // ---- Editor toggles ----
        private void Toolbar_ToggleWrap_Click(object sender, RoutedEventArgs e)
        {
            _wordWrap = !_wordWrap;
            foreach (var ed in AllEditors())
                ed.WordWrap = _wordWrap;
            Status("Word wrap: " + (_wordWrap ? "ON" : "OFF"));
        }

        private void Toolbar_ToggleInvis_Click(object sender, RoutedEventArgs e)
        {
            _showInvisibles = !_showInvisibles;
            foreach (var ed in AllEditors())
            {
                ed.Options.ShowSpaces = _showInvisibles;
                ed.Options.ShowTabs = _showInvisibles;
                ed.Options.ShowEndOfLine = _showInvisibles;
            }
            Status("Invisibles: " + (_showInvisibles ? "ON" : "OFF"));
        }

        private void Toolbar_ThemeCycle_Click(object sender, RoutedEventArgs e)
        {
            // Cycle System → Light → Dark → System
            var next = _settings.ThemeMode switch
            {
                ThemeMode.System => ThemeMode.Light,
                ThemeMode.Light => ThemeMode.Dark,
                _ => ThemeMode.System
            };
            ThemeManager.ApplyTheme(next);
            _settings.ThemeMode = next;
            SaveSettings();

            // Re-tint open editors
            foreach (var ed in AllEditors())
            {
                ThemeManager.ApplyToEditor(ed);
                if (ed.SyntaxHighlighting != null)
                    RetintHighlighting(ed.SyntaxHighlighting, IsDarkThemeActive());
            }
            Status($"Theme: {next}");
        }

        // ---- Navigation helpers ----
        private void Toolbar_GoToDef_Click(object sender, RoutedEventArgs e)
        {
            var ed = ActiveEditor();
            if (ed == null) { Status("No editor"); return; }
            GoToDefinition(ed);
        }

        // ---- Branch switcher ----
        private void BranchCombo_DropDownOpened(object sender, EventArgs e)
        {
            if (_project == null) return;
            try
            {
                var list = GitService.GetBranches(_project.RootPath).ToList();
                BranchCombo.ItemsSource = list;
                var current = GitService.GetRepoInfo(_project.RootPath).Branch;
                if (current != null)
                    BranchCombo.SelectedItem = list.FirstOrDefault(b => string.Equals(b, current, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Log("Branches: " + ex.Message);
            }
        }

        private void BranchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_project == null) return;
            if (BranchCombo.SelectedItem is string name && !string.IsNullOrWhiteSpace(name))
            {
                try
                {
                    GitService.CheckoutBranch(_project.RootPath, name);
                    Log($"Git: checked out {name}.");
                    UpdateGitStatusStrip();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Checkout Branch", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BranchRefresh_Click(object sender, RoutedEventArgs e)
        {
            // Just re-open the dropdown to refresh
            BranchCombo_DropDownOpened(sender, EventArgs.Empty);
            Status("Branches refreshed");
        }

        private void OpenMergeHelper_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;

            var repoRoot = GitService.FindRepoRoot(_project!.RootPath) ?? _project.RootPath;
            var win = new WoWAddonIDE.Windows.MergeHelperWindow(repoRoot) { Owner = this };

            // Use the parameterless method on the window
            win.LoadConflicts();
            win.ShowDialog();

            // Refresh your status after resolving/closing
            UpdateGitStatusStrip();
        }

        private async void GitPublishRelease_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) { MessageBox.Show(this, "Open a project first."); return; }

            var w = new WoWAddonIDE.Windows.ReleasePublisherWindow(_settings) { Owner = this };
            if (w.ShowDialog() == true)
            {
                try
                {
                    var repoPath = GitService.FindRepoRoot(_project.RootPath) ?? _project.RootPath;
                    var zipPath = w.AssetPath;     // path to the built ZIP
                    var tag = w.TagName;       // e.g. v1.2.3
                    var name = w.ReleaseName;   // release title
                    var body = w.Body;          // release notes
                    var prerelease = w.Prerelease;    // bool

                    await GitService.PublishGitHubReleaseAsync(
                        repoPath, _settings, zipPath, tag, name, body, prerelease);

                    Log($"Release created: {w.TagName} (asset: {System.IO.Path.GetFileName(w.AssetPath)})");
                    Status("Release published.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Publish Release", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ---- API Docs: menu handlers ----

        private void ApiDocsReload_Click(object sender, RoutedEventArgs e)
        {
            // Reload the embedded default Resources/wow_api.json
            LoadApiDocs();
            _completion.SetApiDocs(_apiDocs.Keys);
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

        // ---- API Docs: helpers ----

        // Merge new entries into the in-memory model and refresh completion
        private void MergeApiDocs(IEnumerable<ApiEntry> entries)
        {
            int before = _apiDocs.Count;

            foreach (var e in entries)
            {
                if (!string.IsNullOrWhiteSpace(e.name))
                    _apiDocs[e.name] = e; // overwrite/merge by name
            }

            _completion.SetApiDocs(_apiDocs.Keys);
            Status($"API docs merged: {before} → {_apiDocs.Count}");
        }

        // ---------------------------------------------------------------------
        // Robust Lua XSHD loader
        // ---------------------------------------------------------------------
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
                catch (Exception ex) { Log($"Relative resource error: {ex.Message}"); }

                // 2) Pack URI
                try
                {
                    var asmName = Assembly.GetExecutingAssembly().GetName().Name ?? "WoWAddonIDE";
                    var s = Application.GetResourceStream(new Uri($"/{asmName};component/Resources/Lua.xshd", UriKind.Relative))?.Stream;
                    Log(s != null ? "Lua.xshd found via pack URI." : "Pack URI NOT found.");
                    if (s != null) return s;
                }
                catch (Exception ex) { Log($"Pack URI error: {ex.Message}"); }

                // 3) Disk
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var p1 = Path.Combine(baseDir, "Resources", "Lua.xshd");
                    var p2 = Path.Combine(baseDir, "Lua.xshd");

                    if (File.Exists(p1)) { Log($"Lua.xshd found on disk: {p1}"); return File.OpenRead(p1); }
                    if (File.Exists(p2)) { Log($"Lua.xshd found on disk: {p2}"); return File.OpenRead(p2); }
                    Log("Disk probe NOT found (bin/Resources or bin root).");
                }
                catch (Exception ex) { Log($"Disk probe error: {ex.Message}"); }

                // 4) Embedded manifest
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
                catch (Exception ex) { Log($"Manifest probe error: {ex.Message}"); }

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

        // ---------- Git menu handlers ----------
        private void GitAuth_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new WoWAddonIDE.Windows.GitAuthWindow(_settings) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _settings = dlg.Settings;
                SaveSettings();
                Log("Git/GitHub settings saved.");
            }
        }

        private void GitInit_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            try { GitService.InitRepo(_project!.RootPath); Log("Git: repository initialized."); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git Init", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitClone_Click(object sender, RoutedEventArgs e)
        {
            var url = Microsoft.VisualBasic.Interaction.InputBox("Clone URL (HTTPS):", "Git Clone", _settings.GitRemoteUrl);
            if (string.IsNullOrWhiteSpace(url)) return;

            var dlg = new VistaFolderBrowserDialog { Description = "Select target folder to clone into" };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var dest = System.IO.Path.Combine(dlg.SelectedPath, System.IO.Path.GetFileNameWithoutExtension(url.Replace(".git", "")));
                var path = GitService.Clone(url, dest, _settings);
                Log($"Git: cloned into {path}");

                // Optionally open as project
                var toc = Directory.GetFiles(path, "*.toc", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (toc != null)
                {
                    _project = AddonProject.LoadFromDirectory(path);
                    RefreshProjectTree();
                    OpenFileInTab(toc);
                    RebuildSymbolIndex();
                    PathText.Text = $"Project: {_project.RootPath}";
                    Status("Cloned and opened project.");
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git Clone", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitSetRemote_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            var url = Microsoft.VisualBasic.Interaction.InputBox("Remote 'origin' URL:", "Set Remote", _settings.GitRemoteUrl);
            if (string.IsNullOrWhiteSpace(url)) return;
            try
            {
                GitService.EnsureRemote(_project!.RootPath, "origin", url);
                _settings.GitRemoteUrl = url; SaveSettings();
                Log($"Git: origin = {url}");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Set Remote", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            try { foreach (var line in GitService.Status(_project!.RootPath)) Log(line); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git Status", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitCommit_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            try
            {
                var win = new WoWAddonIDE.Windows.GitCommitWindow { Owner = this };
                if (win.ShowDialog() == true)
                {
                    SaveAll_Click(sender, e);
                    GitService.CommitAll(_project!.RootPath, _settings, win.CommitMessage);
                    Log("Git: committed.");
                }
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git Commit", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitPull_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            try { GitService.Pull(_project!.RootPath, _settings); Log("Git: pulled."); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git Pull", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitPush_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            try { GitService.Push(_project!.RootPath, _settings); Log("Git: pushed."); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git Push", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitSync_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            try { GitService.Sync(_project!.RootPath, _settings); Log("Git: sync done (pull + push)."); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Git Sync", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitCreateBranch_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            var name = Microsoft.VisualBasic.Interaction.InputBox("New branch name:", "Create Branch", "feature/new-thing");
            if (string.IsNullOrWhiteSpace(name)) return;
            try { GitService.CreateBranch(_project!.RootPath, name, checkout: true); Log($"Git: created & checked out {name}."); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Create Branch", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitCheckoutBranch_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            try
            {
                var branches = GitService.GetBranches(_project!.RootPath).ToList();
                if (branches.Count == 0) { MessageBox.Show(this, "No branches."); return; }
                var choice = Microsoft.VisualBasic.Interaction.InputBox("Branch to checkout:", "Checkout Branch", branches[0]);
                if (string.IsNullOrWhiteSpace(choice)) return;
                GitService.CheckoutBranch(_project!.RootPath, choice);
                Log($"Git: checked out {choice}.");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Checkout Branch", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void GitOpenOnGitHub_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            try
            {
                var url = GitService.GetOriginUrl(_project!.RootPath) ?? _settings.GitRemoteUrl;
                var web = GitService.ToGitHubWebUrl(url ?? "");
                if (web == null) { MessageBox.Show(this, "Origin is not a GitHub remote."); return; }
                Process.Start(new ProcessStartInfo { FileName = web, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Open on GitHub", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async void GitCreateGitHubRepo_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;

            try
            {
                if (string.IsNullOrWhiteSpace(_settings.GitHubToken))
                {
                    MessageBox.Show(this, "Set your GitHub Token first (Git/GitHub Settings…).",
                        "GitHub", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var repoName = System.IO.Path.GetFileName(_project!.RootPath.TrimEnd('\\', '/'));
                var owner = Microsoft.VisualBasic.Interaction.InputBox(
                    "Owner (leave blank for your user; enter org name to create in an org):",
                    "Create GitHub Repo", "");
                var isPrivate = MessageBox.Show(this, "Create as private repo?", "GitHub",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

                var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("WoWAddonIDE"))
                {
                    Credentials = new Octokit.Credentials(_settings.GitHubToken)
                };

                Octokit.Repository repo;
                var newRepo = new Octokit.NewRepository(repoName) { Private = isPrivate };

                if (string.IsNullOrWhiteSpace(owner))
                    repo = await client.Repository.Create(newRepo);
                else
                    repo = await client.Repository.Create(owner, newRepo);

                var remoteUrl = repo.CloneUrl; // HTTPS
                GitService.EnsureRemote(_project!.RootPath, "origin", remoteUrl);
                _settings.GitRemoteUrl = remoteUrl; SaveSettings();
                Log($"GitHub: repo created at {repo.HtmlUrl}");

                // Initial commit/push
                try { GitService.CommitAll(_project!.RootPath, _settings, "Initial commit"); } catch { /* ignore */ }
                GitService.Push(_project!.RootPath, _settings);
                Process.Start(new ProcessStartInfo { FileName = repo.HtmlUrl, UseShellExecute = true });
            }
            catch (Octokit.AuthorizationException)
            {
                MessageBox.Show(this,
                    "GitHub says: 'Resource not accessible by personal access token'.\n\n" +
                    "Fix it by doing ONE of these:\n" +
                    " • Use a CLASSIC PAT with the 'repo' scope (recommended for creating repos), OR\n" +
                    " • Create the repo in your browser, then Git → Set Remote (origin)… and Push, OR\n" +
                    " • If creating in an organization with SAML SSO, open your PAT on GitHub and click 'Authorize' for that org.",
                    "GitHub authorization", MessageBoxButton.OK, MessageBoxImage.Warning);

                Process.Start(new ProcessStartInfo { FileName = "https://github.com/new", UseShellExecute = true });
            }
            catch (Octokit.ForbiddenException ex)
            {
                MessageBox.Show(this,
                    "GitHub refused the request (Forbidden). If your org enforces SAML SSO, authorize your token for that org.\n\n" +
                    ex.Message, "GitHub", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Create GitHub Repo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void GitSignIn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.GitHubOAuthClientId))
            {
                MessageBox.Show(this,
                    "GitHub OAuth Client ID is empty. Go to Git → Git/GitHub Settings… and paste the Client ID from your OAuth App.",
                    "GitHub Sign-in", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var w = new WoWAddonIDE.Windows.GitHubSignInWindow(_settings.GitHubOAuthClientId) { Owner = this };
            if (w.ShowDialog() == true && !string.IsNullOrWhiteSpace(w.AccessToken))
            {
                _settings.GitHubToken = w.AccessToken!;
                SaveSettings();
                Log("Signed in to GitHub. OAuth token stored.");
            }
        }

        private void GitOpenDotGit_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            var path = System.IO.Path.Combine(_project!.RootPath, ".git");
            if (!Directory.Exists(path)) { MessageBox.Show(this, "No .git folder here."); return; }
            Process.Start("explorer.exe", path);
        }

        // ------------------------------------------------
        // WoW API docs loader for hover tooltips
        // ------------------------------------------------
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

        // ---------- SAFE Build to AddOns ----------
        private void Build_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            SaveAll_Click(sender, e);

            foreach (var p in LuaLint.Pass(_project!)) Log(p);

            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set or invalid. Use Tools > Settings.", "Build", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var src = Path.GetFullPath(_project!.RootPath);
                var addons = Path.GetFullPath(_settings.AddOnsPath);
                var target = Path.GetFullPath(Path.Combine(addons, _project!.Name));

                var sourceInsideAddons = src.StartsWith(addons, StringComparison.OrdinalIgnoreCase);
                if (sourceInsideAddons && !_settings.AllowBuildInsideAddOns)
                {
                    MessageBox.Show(this,
                        "Your project folder is already inside AddOns.\n" +
                        "To avoid data loss, Build is disabled.\n\n" +
                        "You can enable it in Tools > Settings (not recommended).",
                        "Build Skipped", MessageBoxButton.OK, MessageBoxImage.Information);
                    Log("Build skipped: project resides inside AddOns.");
                    return;
                }

                if (string.Equals(src, target, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "Source and target are the same folder. Aborting.", "Build Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Clean target only when it's separate from source
                if (Directory.Exists(target)) Directory.Delete(target, true);
                Directory.CreateDirectory(target);

                CopyDirectorySafe(src, target);
                Status("Build succeeded (copied to AddOns).");
                Log($"Build copied to: {target}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Build Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildToFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            SaveAll_Click(sender, e);

            var dlg = new VistaFolderBrowserDialog { Description = "Choose a folder to build the addon into." };
            if (dlg.ShowDialog(this) != true) return;

            var src = Path.GetFullPath(_project!.RootPath);
            var target = Path.Combine(dlg.SelectedPath, _project!.Name);
            if (string.Equals(src, Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "Source and target are the same folder. Pick a different destination.", "Build to Folder",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(target);
            CopyDirectorySafe(src, target);

            Status("Build to folder completed.");
            Log($"Build copied to: {target}");
        }

        private void BuildZip_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            SaveAll_Click(sender, e);

            try
            {
                var src = Path.GetFullPath(_project!.RootPath);
                var staging = string.IsNullOrWhiteSpace(_settings.StagingPath)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WoWAddonIDE", "Staging")
                    : _settings.StagingPath;

                Directory.CreateDirectory(staging);

                var tempFolder = Path.Combine(staging, "_build_temp_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempFolder);

                var stageTarget = Path.Combine(tempFolder, _project!.Name);
                CopyDirectorySafe(src, stageTarget, _settings.PackageExcludes);

                var zipPath = Path.Combine(staging, $"{_project!.Name}-{DateTime.Now:yyyyMMdd-HHmm}.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempFolder, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

                try { Directory.Delete(tempFolder, true); } catch { /* ignore */ }

                Status("Build Zip completed.");
                Log($"Package written: {zipPath}");
                Process.Start("explorer.exe", $"/select,\"{zipPath}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Build Zip Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Live Reload ----------
        private void LiveReload_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) return;
            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath) || !Directory.Exists(_settings.AddOnsPath))
            {
                MessageBox.Show(this, "AddOns path is not set. Tools > Settings.", "Live Reload",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                var addonDir = Path.Combine(_settings.AddOnsPath, _project.Name);
                Directory.CreateDirectory(addonDir);
                var flag = Path.Combine(addonDir, "Reload.flag");
                File.WriteAllText(flag, DateTime.UtcNow.ToString("o"));
                Status("Live reload flag written.");
                Log($"Live reload flag: {flag}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Live Reload Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            Process.Start("explorer.exe", _settings.AddOnsPath);
        }

        private void OpenStagingFolder_Click(object sender, RoutedEventArgs e)
        {
            var staging = string.IsNullOrWhiteSpace(_settings.StagingPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WoWAddonIDE", "Staging")
                : _settings.StagingPath;

            try { Directory.CreateDirectory(staging); } catch { /* ignore */ }
            Process.Start("explorer.exe", staging);
        }

        private void Clean_Click(object sender, RoutedEventArgs e) => Output.Clear();

        // ========================= Tools Menu ========================

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(_settings) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                _settings = dlg.Settings;
                SaveSettings();
                PathText.Text = $"AddOns Path: {_settings.AddOnsPath}";
                Log("Settings saved.");
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
                "• Hover docs (WoW API)\n" +
                "• Project explorer + TOC generator\n" +
                "• Safe Build to AddOns / Build to Folder / Build Zip\n" +
                "• Find in Files (Ctrl+Shift+F), Outline, Toggle comment (Ctrl+/), Duplicate line (Ctrl+D)\n",
                "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ====================== Project Tree / Tabs ======================

        private void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) { /* optional */ }

        private void ProjectTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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

            var rootItem = new TreeViewItem
            {
                Header = _project.Name,
                Tag = _project.RootPath,
                IsExpanded = true
            };

            // Primary TOC
            string? primaryToc = File.Exists(_project.TocPath)
                ? Path.GetFullPath(_project.TocPath)
                : null;

            if (primaryToc != null)
            {
                rootItem.Items.Add(new TreeViewItem
                {
                    Header = System.IO.Path.GetFileName(primaryToc),
                    Tag = primaryToc
                });
            }

            void AddDir(TreeViewItem parent, string dir)
            {
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

                foreach (var file in Directory.GetFiles(dir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    // Skip the primary TOC we already inserted at the top
                    if (primaryToc != null &&
                        string.Equals(Path.GetFullPath(file), primaryToc, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Hide images (optional)
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
            ProjectTree.Items.Add(rootItem);
        }

        // ============================ TOC Editor ============================
        private void TocEditor_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null || string.IsNullOrWhiteSpace(_project.TocPath) || !File.Exists(_project.TocPath))
            {
                MessageBox.Show(this, "No TOC found in this project."); return;
            }
            var w = new WoWAddonIDE.Windows.TocEditorWindow(_project.TocPath) { Owner = this };
            if (w.ShowDialog() == true)
            {
                Log("TOC saved.");
                OpenFileInTab(_project.TocPath);
            }
        }

        // ============================ Diff Viewer ============================
        private void ShowDiff_Click(object sender, RoutedEventArgs e)
        {
            if (EditorTabs.SelectedItem is not TabItem tab || tab.Content is not TextEditor ed) return;
            if (tab.Tag is not string path || !File.Exists(path)) return;

            var disk = File.ReadAllText(path);
            var buf = ed.Text;

            var dw = new WoWAddonIDE.Windows.DiffWindow { Owner = this };
            dw.ShowDiff(disk, buf);
            dw.ShowDialog();
        }

        // ============================ Editors ============================

        private void OpenFileInTab(string path)
        {
            // Focus if already open
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

            ThemeManager.ApplyToEditor(editor);

            // QoL
            editor.Options.HighlightCurrentLine = true;
            editor.Options.IndentationSize = 4;
            editor.Options.ConvertTabsToSpaces = true;

            // Syntax highlighting
            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                var def = _completion.LuaHighlight ?? HighlightingManager.Instance.GetDefinition("Lua");
                editor.SyntaxHighlighting = def;
                RetintHighlighting(def, IsDarkThemeActive());
            }
            else if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
            }
            else if (path.EndsWith(".toc", StringComparison.OrdinalIgnoreCase))
            {
                var def = HighlightingManager.Instance.GetDefinition("WoWTOC");
                editor.SyntaxHighlighting = def;
                RetintHighlighting(def, IsDarkThemeActive());
            }

            // Load file
            editor.Text = File.ReadAllText(path);

            // Inline diagnostics (Lua only)
            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                var diag = new LuaDiagnosticsTransformer();
                editor.TextArea.TextView.LineTransformers.Add(diag);
                editor.TextChanged += (s, e) => diag.Reanalyze(editor.Text);
                diag.Reanalyze(editor.Text);
            }

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

            // Hover docs
            var tv = editor.TextArea.TextView;
            tv.MouseHover += (s, e) =>
            {
                var pos = tv.GetPositionFloor(e.GetPosition(tv));
                if (!pos.HasValue) { editor.ToolTip = null; return; }

                int offset = editor.Document.GetOffset(pos.Value.Location);
                var word = GetWordAtOffset(editor.Text, offset);
                if (string.IsNullOrWhiteSpace(word)) { editor.ToolTip = null; return; }

                if (_apiDocs.TryGetValue(word, out var entry))
                {
                    var text = $"{entry.name}\n{entry.signature}\n{entry.description}";
                    var tt = new ToolTip { Content = text };
                    ToolTipService.SetShowDuration(tt, 20000);
                    editor.ToolTip = tt;
                }
                else
                {
                    editor.ToolTip = null;
                }
            };
            tv.MouseHoverStopped += (s, e) => { editor.ToolTip = null; };

            // Track changes + outline refresh
            editor.TextChanged += (s, e) =>
            {
                MarkTabDirty(path, true);
                if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                    RefreshOutlineForActive();
            };

            // Editor commands
            editor.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => ToggleComment(editor)),
                new KeyGesture(Key.Oem2, ModifierKeys.Control))); // Ctrl+/

            editor.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => DuplicateLine(editor)),
                new KeyGesture(Key.D, ModifierKeys.Control))); // Ctrl+D

            // F12: Go to Definition
            editor.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => GoToDefinition(editor)),
                new KeyGesture(Key.F12)));

            var tabItem = new TabItem
            {
                Header = System.IO.Path.GetFileName(path),
                Content = editor,
                Tag = path
            };

            EditorTabs.Items.Add(tabItem);
            EditorTabs.SelectedItem = tabItem;

            // When opening a TOC, validate and log issues
            if (path.EndsWith(".toc", StringComparison.OrdinalIgnoreCase))
                foreach (var m in ValidateToc(path)) Log(m);

            RefreshOutlineForActive();
        }

        // ---------------------- Go to Definition ----------------------
        private void GoToDefinition(TextEditor ed)
        {
            if (_project == null) return;
            if ((DateTime.Now - _symbolIndexBuilt).TotalSeconds > 10) RebuildSymbolIndex();

            var word = GetWordAtOffset(ed.Text, ed.CaretOffset);
            if (string.IsNullOrWhiteSpace(word)) return;

            if (_symbolIndex.TryGetValue(word, out var locs) && locs.Count > 0)
            {
                var l = locs[0];
                OpenFileInTab(l.File);
                if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor target)
                {
                    target.ScrollToLine(l.Line);
                    target.CaretOffset = Math.Min(target.Document.TextLength, target.Document.GetOffset(l.Line, 1));
                    target.Focus();
                }
            }
            else
            {
                Status($"Definition not found: {word}");
            }
        }

        // ---------------------- Theme Application ----------------------
        private void ApplyThemeToEditor(TextEditor ed)
        {
            ThemeManager.ApplyToEditor(ed);
            if (ed.SyntaxHighlighting != null)
                RetintHighlighting(ed.SyntaxHighlighting, IsDarkThemeActive());
        }

        // ---------------------- Theme Menu Clicks ----------------------
        private void ThemeSystem_Click(object s, RoutedEventArgs e) { ThemeManager.ApplyTheme(ThemeMode.System); SaveSettings(); }
        private void ThemeLight_Click(object s, RoutedEventArgs e) { ThemeManager.ApplyTheme(ThemeMode.Light); SaveSettings(); }
        private void ThemeDark_Click(object s, RoutedEventArgs e) { ThemeManager.ApplyTheme(ThemeMode.Dark); SaveSettings(); }

        // ========================= Symbol Index =========================
        private void RebuildSymbolIndex()
        {
            if (_project == null) return;
            _symbolIndex = SymbolService.BuildIndex(_project.RootPath);
            _symbolIndexBuilt = DateTime.Now;
            Log($"Symbol index: {_symbolIndex.Count} symbols.");
        }

        // ========================= TOC Validation =========================
        private IEnumerable<string> ValidateToc(string tocPath)
        {
            var msgs = new List<string>();
            if (!File.Exists(tocPath)) { msgs.Add("TOC not found."); return msgs; }

            var root = Path.GetDirectoryName(tocPath)!;
            var lines = File.ReadAllLines(tocPath);
            var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requiredKeys = new[] { "Interface", "Title" };
            var foundKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                // directives: ## Key: Value
                if (line.StartsWith("##"))
                {
                    var idx = line.IndexOf(':');
                    if (idx > 2)
                    {
                        var key = line.Substring(2, idx - 2).Trim();
                        foundKeys.Add(key);
                    }
                    continue;
                }

                // key: value
                var colon = line.IndexOf(':');
                if (colon > 0 && !line.Contains("\\") && !line.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) && !line.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    var key = line.Substring(0, colon).Trim();
                    foundKeys.Add(key);
                    continue;
                }

                // file reference
                if (line.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) || line.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    var full = Path.GetFullPath(Path.Combine(root, line.Replace('/', Path.DirectorySeparatorChar)));
                    if (!File.Exists(full))
                        msgs.Add($"Missing file (L{i + 1}): {line}");
                    else
                        seenFiles.Add(full);
                }
            }

            foreach (var req in requiredKeys)
                if (!foundKeys.Contains(req))
                    msgs.Add($"Missing required key: {req}");

            if (seenFiles.Count == 0) msgs.Add("TOC lists no Lua/XML files.");

            return msgs;
        }

        // ---------------------- TOC Highlighting ----------------------
        private void EnsureTocHighlightRegistered()
        {
            if (HighlightingManager.Instance.GetDefinition("WoWTOC") != null)
            {
                Log("TOC highlight already registered.");
                return;
            }

            Stream? TryOpen()
            {
                // 1) Relative WPF resource (case-sensitive path)
                try
                {
                    foreach (var candidate in new[]
                    {
                        "Resources/WoWTOC.xshd",
                        "Resources/wowtoc.xshd",
                        "Resources/WowToc.xshd"
                    })
                    {
                        var s = Application.GetResourceStream(new Uri(candidate, UriKind.Relative))?.Stream;
                        if (s != null) { Log($"WoWTOC.xshd found via relative resource: {candidate}"); return s; }
                    }
                    Log("Relative resource NOT found for WoWTOC.xshd.");
                }
                catch (Exception ex) { Log($"Relative resource error (TOC): {ex.Message}"); }

                // 2) Pack URI
                try
                {
                    var asmName = Assembly.GetExecutingAssembly().GetName().Name ?? "WoWAddonIDE";
                    foreach (var candidate in new[]
                    {
                        $"/{asmName};component/Resources/WoWTOC.xshd",
                        $"/{asmName};component/Resources/wowtoc.xshd",
                        $"/{asmName};component/Resources/WowToc.xshd"
                    })
                    {
                        var s = Application.GetResourceStream(new Uri(candidate, UriKind.Relative))?.Stream;
                        if (s != null) { Log($"WoWTOC.xshd found via pack URI: {candidate}"); return s; }
                    }
                    Log("Pack URI NOT found for WoWTOC.xshd.");
                }
                catch (Exception ex) { Log($"Pack URI error (TOC): {ex.Message}"); }

                // 3) Disk fallbacks
                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    foreach (var p in new[]
                    {
                        Path.Combine(baseDir, "Resources", "WoWTOC.xshd"),
                        Path.Combine(baseDir, "Resources", "wowtoc.xshd"),
                        Path.Combine(baseDir, "WoWTOC.xshd"),
                        Path.Combine(baseDir, "wowtoc.xshd")
                    })
                    {
                        if (File.Exists(p)) { Log($"WoWTOC.xshd found on disk: {p}"); return File.OpenRead(p); }
                    }
                    Log("Disk probe NOT found for WoWTOC.xshd.");
                }
                catch (Exception ex) { Log($"Disk probe error (TOC): {ex.Message}"); }

                // 4) Embedded manifest
                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    var resName = asm.GetManifestResourceNames()
                                     .FirstOrDefault(n => n.EndsWith("WoWTOC.xshd", StringComparison.OrdinalIgnoreCase));
                    if (resName != null)
                    {
                        Log($"WoWTOC.xshd found as manifest resource: {resName}");
                        return asm.GetManifestResourceStream(resName);
                    }
                    Log("No manifest resource named *WoWTOC.xshd* found.");
                }
                catch (Exception ex) { Log($"Manifest probe error (TOC): {ex.Message}"); }

                return null;
            }

            try
            {
                using var stream = TryOpen();
                if (stream == null)
                {
                    Log("WoWTOC.xshd still not found after all probes.");
                    return;
                }

                using var reader = new XmlTextReader(stream);
                var def = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting("WoWTOC", new[] { ".toc" }, def);
                Log("TOC highlighting registered.");
            }
            catch (Exception ex)
            {
                Log($"Failed to register TOC highlighting: {ex.Message}");
            }
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

        private void RetintHighlighting(IHighlightingDefinition def, bool dark)
        {
            if (def == null) return;

            var named = def.NamedHighlightingColors;

            ICSharpCode.AvalonEdit.Highlighting.HighlightingColor? Find(string name) =>
                named.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

            void Set(string name, byte r, byte g, byte b)
            {
                var hc = Find(name);
                if (hc != null)
                {
                    hc.Foreground = new ICSharpCode.AvalonEdit.Highlighting.SimpleHighlightingBrush(
                        System.Windows.Media.Color.FromRgb(r, g, b));
                }
            }

            if (dark)
            {
                Set("Comment", 0x6A, 0x99, 0x55);
                Set("String", 0xCE, 0x91, 0x78);
                Set("Number", 0xB5, 0xCE, 0xA8);
                Set("Keyword", 0x56, 0x9C, 0xD6);
            }
        }

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

        private void Outline_DoubleClick(object sender, MouseButtonEventArgs e)
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

        // -------------------- Copy helper & excludes --------------------
        private void CopyDirectorySafe(string src, string dest, string? excludes = null)
        {
            var excludeSet = BuildExcludeSet(excludes);
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);
                if (excludeSet.Contains(ext)) continue;

                var rel = Path.GetRelativePath(src, file);
                var outPath = Path.Combine(dest, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                File.Copy(file, outPath, true);
            }
        }

        private static HashSet<string> BuildExcludeSet(string? excludes)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(excludes))
            {
                var parts = excludes.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    var ext = p.Trim();
                    if (!ext.StartsWith(".")) ext = "." + ext;
                    set.Add(ext);
                }
            }
            return set;
        }

        // -------------------- Editor helpers --------------------
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
                        var idxInLine = leading + trimmed.IndexOf("--", StringComparison.Ordinal);
                        doc.Remove(line.Offset + idxInLine, 2);
                    }
                    else
                    {
                        doc.Insert(line.Offset + leading, "--");
                    }

                    if (line == endLine) break;
                    line = line.NextLine!;
                }
            }
        }

        private void GoToSymbol_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) return;

            if ((DateTime.Now - _symbolIndexBuilt).TotalSeconds > 10)
                RebuildSymbolIndex();

            var dlg = new WoWAddonIDE.Windows.SymbolSearchWindow
            {
                Owner = this,
                Index = _symbolIndex.ToDictionary(k => k.Key, v => v.Value)
            };

            dlg.NavigateTo += loc =>
            {
                OpenFileInTab(loc.File);
                if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed)
                {
                    ed.ScrollToLine(loc.Line);
                    ed.CaretOffset = Math.Min(ed.Document.TextLength, ed.Document.GetOffset(loc.Line, 1));
                    ed.Focus();
                }
            };

            dlg.ShowDialog();
        }

        private void DuplicateLine(TextEditor ed)
        {
            var doc = ed.Document;
            var line = doc.GetLineByOffset(ed.CaretOffset);
            var text = doc.GetText(line.Offset, line.TotalLength);
            doc.Insert(line.EndOffset, text);
        }

        private static string GetWordAtOffset(string text, int offset)
        {
            if (offset < 0 || offset > text.Length) return "";
            int start = offset;
            int end = offset;

            bool IsWordChar(char c) =>
                char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == ':'; // allow table.member and method:Call

            if (start > 0 && !IsWordChar(text[start - 1]) && start < text.Length && IsWordChar(text[start])) end++;

            while (start > 0 && IsWordChar(text[start - 1])) start--;
            while (end < text.Length && IsWordChar(text[end])) end++;

            return start < end ? text.Substring(start, end - start) : "";
        }

        // -------------------- API docs model --------------------
        private class ApiEntry
        {
            public string name { get; set; } = "";
            public string signature { get; set; } = "";
            public string description { get; set; } = "";
        }

        // ====================================================================
        // CLICK HANDLER STUBS (so XAML compiles). They call your real methods.
        // ====================================================================

        // File
        private void New_Click(object s, RoutedEventArgs e) => NewProject_Click(s, e);
        private void Open_Click(object s, RoutedEventArgs e) => OpenProject_Click(s, e);

        // Edit
        private void Find_Click(object s, RoutedEventArgs e)
        {
            // simple inline find (falls back to Find in Files if no editor)
            if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed)
            {
                var q = Interaction.InputBox("Find text:", "Find", ed.SelectedText ?? "");
                if (!string.IsNullOrEmpty(q))
                {
                    var idx = ed.Text.IndexOf(q, ed.CaretOffset, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) idx = ed.Text.IndexOf(q, 0, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        ed.Select(idx, q.Length);
                        ed.ScrollToLine(ed.Document.GetLineByOffset(idx).LineNumber);
                        ed.Focus();
                        Status($"Found: {q}");
                    }
                    else Status($"'{q}' not found");
                }
            }
            else
            {
                FindInFiles_Click(s, e);
            }
        }

        // Project
        private void OpenAddonsFolder_Click(object s, RoutedEventArgs e) => OpenAddOnsFolder_Click(s, e);

        // Toolbar buttons already point to existing handlers:
        // Save_Click, SaveAll_Click, Build_Click, BuildZip_Click are defined above.
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
}