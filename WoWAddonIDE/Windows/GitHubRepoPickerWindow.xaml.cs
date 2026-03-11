// File: WoWAddonIDE/Windows/GitHubRepoPickerWindow.xaml.cs
using Octokit;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;               // ✅ needed for Directory.GetParent, Path, etc.
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class GitHubRepoPickerWindow : Window
    {
        private readonly Models.IDESettings _settings;
        private readonly string? _currentProjectPath;
        private readonly ObservableCollection<RepoItem> _all = new();
        private readonly ObservableCollection<RepoItem> _view = new();

        public string? ResultClonedPath { get; private set; }
        public string? ResultOpenedLocalPath { get; private set; }
        public string? ResultInitializedRemoteUrl { get; private set; }

        public GitHubRepoPickerWindow(Models.IDESettings settings, string? currentProjectPath = null)
        {
            InitializeComponent();
            _settings = settings;
            _currentProjectPath = currentProjectPath;

            Grid.ItemsSource = _view;
            InitBtn.IsEnabled = !string.IsNullOrWhiteSpace(_currentProjectPath);
            _ = RefreshAsync();
        }

        private GitHubClient MakeClient()
        {
            if (string.IsNullOrWhiteSpace(_settings.GitHubToken))
                throw new InvalidOperationException("You are not signed in to GitHub.");
            return new GitHubClient(new ProductHeaderValue("WoWAddonIDE"))
            {
                Credentials = new Credentials(_settings.GitHubToken)
            };
        }

        private async System.Threading.Tasks.Task RefreshAsync()
        {
            try
            {
                var gh = MakeClient();

                // User repos
                var userRepos = await gh.Repository.GetAllForCurrent(new ApiOptions { PageSize = 100 });

                // Orgs and their repos
                var all = new List<Repository>(userRepos);
                var orgs = await gh.Organization.GetAllForCurrent();
                foreach (var org in orgs)
                {
                    var orgRepos = await gh.Repository.GetAllForOrg(org.Login, new ApiOptions { PageSize = 100 });
                    all.AddRange(orgRepos);
                }

                var items = all
                    .OrderByDescending(r => r.UpdatedAt)
                    .Select(r => new RepoItem(r))
                    .ToList();

                _all.Clear();
                foreach (var it in items) _all.Add(it);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "GitHub", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            var q = (SearchBox.Text ?? "").Trim();
            _view.Clear();
            IEnumerable<RepoItem> src = _all;
            if (!string.IsNullOrEmpty(q))
            {
                src = src.Where(r =>
                    (r.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.Owner?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (r.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            foreach (var it in src) _view.Add(it);
        }

        private RepoItem? Selected() => Grid.SelectedItem as RepoItem;

        // --- UI events ---

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private void Grid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Selected() != null) Clone_Click(sender, e);
        }

        private void Clone_Click(object sender, RoutedEventArgs e)
        {
            var sel = Selected();
            if (sel == null)
            {
                MessageBox.Show(this, "Pick a repository to clone.", "GitHub", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog { Description = "Select folder to clone into" };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var dest = System.IO.Path.Combine(dlg.SelectedPath, sel.Name);

                // Clone and be defensive about what libgit2 returns
                var cloneReturn = WoWAddonIDE.Services.GitService.Clone(sel.CloneUrl, dest, _settings);

                var workdir = cloneReturn;
                if (workdir.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    workdir = System.IO.Directory.GetParent(workdir)!.FullName;

                // ✅ Safest: always tell the caller to open the working directory we asked for
                ResultClonedPath = dest;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Clone", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLocal_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new VistaFolderBrowserDialog { Description = "Open existing local project (folder with .toc preferred)" };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                // ✅ Normalize in case the user accidentally picks a .git folder
                var selected = dlg.SelectedPath;
                if (selected.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    selected = Directory.GetParent(selected)!.FullName;

                ResultOpenedLocalPath = selected;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Open project", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Init_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentProjectPath))
            {
                MessageBox.Show(this, "Open a project first.", "Initialize", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var gh = MakeClient();

                var repoName = Path.GetFileName(_currentProjectPath.TrimEnd('\\', '/'));
                var owner = Microsoft.VisualBasic.Interaction.InputBox(
                    "Owner (leave blank to create under your user; enter org login to create in an org):",
                    "Initialize on GitHub", "");
                var makePrivate = MessageBox.Show(this, "Create as private repo?", "GitHub",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

                var newRepo = new NewRepository(repoName) { Private = makePrivate, AutoInit = false };
                Repository repo = string.IsNullOrWhiteSpace(owner)
                    ? await gh.Repository.Create(newRepo)
                    : await gh.Repository.Create(owner, newRepo);

                GitService.EnsureRemote(_currentProjectPath, "origin", repo.CloneUrl);
                try { GitService.CommitAll(_currentProjectPath, _settings, "Initial commit"); } catch { /* ignore */ }
                GitService.Push(_currentProjectPath, _settings);

                ResultInitializedRemoteUrl = repo.HtmlUrl;
                DialogResult = true;
                Close();
            }
            catch (Octokit.AuthorizationException)
            {
                MessageBox.Show(this,
                    "GitHub refused the request. Ensure your token has the 'repo' scope and (if needed) SAML is authorized for your org.",
                    "GitHub", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Initialize on GitHub", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- model ---

        public sealed class RepoItem
        {
            public string Owner { get; }
            public string Name { get; }
            public bool Private { get; }
            public string Description { get; }
            public DateTimeOffset UpdatedAt { get; }
            public string UpdatedDisplay => UpdatedAt.ToLocalTime().ToString("g");
            public string CloneUrl { get; }
            public string HtmlUrl { get; }

            public RepoItem(Repository r)
            {
                Owner = r.Owner?.Login ?? "";
                Name = r.Name ?? "";
                Private = r.Private;
                Description = r.Description ?? "";

                // Compute a sensible "updated" value without using '??' on non-nullables
                var updated = r.CreatedAt;
                if (r.UpdatedAt is DateTimeOffset u1 && u1 > updated) updated = u1;
                if (r.PushedAt is DateTimeOffset p1 && p1 > updated) updated = p1;
                UpdatedAt = updated;

                CloneUrl = r.CloneUrl;
                HtmlUrl = r.HtmlUrl;
            }
        }
    }
}