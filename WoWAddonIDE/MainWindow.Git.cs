using Microsoft.VisualBasic;
using Ookii.Dialogs.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
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

            var w = new Windows.BlameWindow { Owner = this };
            w.LoadBlame(_project.RootPath, path);
            w.ShowDialog();
        }

        private void GitHistoryActive_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) { MessageBox.Show(this, "Open a project first."); return; }
            if (EditorTabs.SelectedItem is not TabItem tab || tab.Tag is not string path) { MessageBox.Show(this, "Open a file tab."); return; }
            if (!File.Exists(path)) { MessageBox.Show(this, "File not found on disk."); return; }

            var w = new Windows.HistoryWindow { Owner = this };
            w.LoadHistory(_project.RootPath, path);
            w.ShowDialog();
        }

        private void GitMergeHelper_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) { MessageBox.Show(this, "Open a project first."); return; }

            var repoRoot = GitService.FindRepoRoot(_project.RootPath) ?? _project.RootPath;
            var w = new Windows.MergeHelperWindow(repoRoot) { Owner = this };

            w.LoadConflicts();
            w.ShowDialog();

            UpdateGitStatusStrip();
        }

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
            BranchCombo_DropDownOpened(sender, EventArgs.Empty);
            Status("Branches refreshed");
        }

        private void OpenMergeHelper_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;

            var repoRoot = GitService.FindRepoRoot(_project!.RootPath) ?? _project.RootPath;
            var win = new Windows.MergeHelperWindow(repoRoot) { Owner = this };

            win.LoadConflicts();
            win.ShowDialog();

            UpdateGitStatusStrip();
        }

        private async void GitPublishRelease_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) { MessageBox.Show(this, "Open a project first."); return; }

            var w = new Windows.ReleasePublisherWindow(_settings) { Owner = this };
            if (w.ShowDialog() == true)
            {
                try
                {
                    var repoPath = GitService.FindRepoRoot(_project.RootPath) ?? _project.RootPath;
                    var zipPath = w.AssetPath;
                    var tag = w.TagName;
                    var name = w.ReleaseName;
                    var body = w.Body;
                    var prerelease = w.Prerelease;

                    await GitService.PublishGitHubReleaseAsync(
                        repoPath, _settings, zipPath, tag, name, body, prerelease);

                    Log($"Release created: {w.TagName} (asset: {Path.GetFileName(w.AssetPath)})");
                    Status("Release published.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Publish Release", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GitAuth_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Windows.GitAuthWindow(_settings) { Owner = this };
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
            var url = Interaction.InputBox("Clone URL (HTTPS):", "Git Clone", _settings.GitRemoteUrl);
            if (string.IsNullOrWhiteSpace(url)) return;

            var dlg = new VistaFolderBrowserDialog { Description = "Select target folder to clone into" };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var dest = Path.Combine(dlg.SelectedPath, Path.GetFileNameWithoutExtension(url.Replace(".git", "")));
                var path = GitService.Clone(url, dest, _settings);
                Log($"Git: cloned into {path}");

                var toc = Directory.GetFiles(path, "*.toc", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (toc != null)
                {
                    _project = Models.AddonProject.LoadFromDirectory(path);
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
            var url = Interaction.InputBox("Remote 'origin' URL:", "Set Remote", _settings.GitRemoteUrl);
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
                var win = new Windows.GitCommitWindow { Owner = this };
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
            var name = Interaction.InputBox("New branch name:", "Create Branch", "feature/new-thing");
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
                var choice = Interaction.InputBox("Branch to checkout:", "Checkout Branch", branches[0]);
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

                var repoName = Path.GetFileName(_project!.RootPath.TrimEnd('\\', '/'));
                var owner = Interaction.InputBox(
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

            var w = new Windows.GitHubSignInWindow(_settings.GitHubOAuthClientId) { Owner = this };
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
            var path = Path.Combine(_project!.RootPath, ".git");
            if (!Directory.Exists(path)) { MessageBox.Show(this, "No .git folder here."); return; }
            Process.Start("explorer.exe", path);
        }
    }
}