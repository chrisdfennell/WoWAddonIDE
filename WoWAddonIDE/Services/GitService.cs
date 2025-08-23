using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Services
{
    public static class GitService
    {
        // ---------- Basics ----------
        public static bool IsRepository(string path) =>
            !string.IsNullOrWhiteSpace(path) && (Repository.IsValid(path) || Directory.Exists(Path.Combine(path, ".git")));

        public static void InitRepo(string root)
        {
            if (IsRepository(root)) return;
            Repository.Init(root);
            using var repo = new Repository(root);
        }

        public static Repository Open(string root) => new Repository(root);

        // ---------- Status ----------
        public static IEnumerable<string> Status(string root)
        {
            using var repo = Open(root);
            var status = repo.RetrieveStatus(new StatusOptions());
            foreach (var e in status)
                yield return $"{e.FilePath}  [{e.State}]";
        }

        public static void StageAll(string root)
        {
            using var repo = Open(root);
            Commands.Stage(repo, "*");
        }

        // ---------- Commit / Pull / Push ----------
        public static void CommitAll(string root, IDESettings s, string message)
        {
            using var repo = Open(root);
            Commands.Stage(repo, "*");

            var sig = EnsureSignature(s);
            EnsureRepoConfig(repo, s);

            repo.Commit(string.IsNullOrWhiteSpace(message) ? "Update" : message, sig, sig);
        }

        public static void EnsureRemote(string root, string name, string url)
        {
            using var repo = Open(root);
            var existing = repo.Network.Remotes[name];
            if (existing == null) repo.Network.Remotes.Add(name, url);
            else repo.Network.Remotes.Update(name, r => r.Url = url);
        }

        public static void Pull(string root, IDESettings s)
        {
            using var repo = Open(root);
            var sig = EnsureSignature(s);
            var opts = new PullOptions
            {
                FetchOptions = new FetchOptions { CredentialsProvider = (_url, _user, _cred) => GetCreds(s) },
                MergeOptions = new MergeOptions { FileConflictStrategy = CheckoutFileConflictStrategy.Theirs }
            };
            Commands.Pull(repo, sig, opts);
        }

        public static void Push(string root, IDESettings s, string remoteName = "origin")
        {
            using var repo = Open(root);
            var opts = new PushOptions { CredentialsProvider = (_url, _user, _cred) => GetCreds(s) };

            if (repo.Info.IsHeadDetached)
                throw new InvalidOperationException("HEAD is detached; checkout a branch first.");

            // Push current branch to its configured upstream (or origin/<branch>)
            var branch = repo.Head;
            repo.Network.Push(branch, opts);
        }

        // ---------- Remote / Clone ----------
        public static string Clone(string url, string targetPath, IDESettings s)
        {
            var opts = new CloneOptions();
            // Configure the existing FetchOptions instance (property is read-only)
            opts.FetchOptions.CredentialsProvider = (_url, _user, _cred) => GetCreds(s);

            return Repository.Clone(url, targetPath, opts);
        }

        // ---------- Branching ----------
        public static void CreateBranch(string root, string name, bool checkout)
        {
            using var repo = Open(root);
            var b = repo.CreateBranch(name);
            if (checkout)
                Commands.Checkout(repo, b);
        }

        public static void CheckoutBranch(string root, string name)
        {
            using var repo = Open(root);
            var b = repo.Branches[name] ?? throw new InvalidOperationException($"Branch not found: {name}");
            Commands.Checkout(repo, b);
        }

        // ---------- Helpers ----------
        public static Credentials GetCreds(IDESettings s)
        {
            if (!string.IsNullOrWhiteSpace(s.GitHubToken))
            {
                var user = string.IsNullOrWhiteSpace(s.GitUserName) ? "git" : s.GitUserName;
                return new UsernamePasswordCredentials { Username = user, Password = s.GitHubToken };
            }
            return new DefaultCredentials();
        }

        public static void Sync(string root, IDESettings s)
        {
            Pull(root, s);
            Push(root, s);
        }

        private static Signature EnsureSignature(IDESettings s)
        {
            var name = string.IsNullOrWhiteSpace(s.GitUserName) ? Environment.UserName : s.GitUserName;
            var email = string.IsNullOrWhiteSpace(s.GitUserEmail) ? $"{Environment.UserName}@localhost" : s.GitUserEmail;
            return new Signature(name, email, DateTimeOffset.Now);
        }

        private static void EnsureRepoConfig(Repository repo, IDESettings s)
        {
            if (!string.IsNullOrWhiteSpace(s.GitUserName)) repo.Config.Set("user.name", s.GitUserName);
            if (!string.IsNullOrWhiteSpace(s.GitUserEmail)) repo.Config.Set("user.email", s.GitUserEmail);
        }

        public static string? GetOriginUrl(string root)
        {
            using var repo = Open(root);
            return repo.Network.Remotes["origin"]?.Url;
        }

        public static IEnumerable<string> GetBranches(string root)
        {
            using var repo = Open(root);
            return repo.Branches.Select(b => b.FriendlyName);
        }

        public static string? ToGitHubWebUrl(string remoteUrl)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl)) return null;
            if (remoteUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase))
            {
                if (remoteUrl.StartsWith("git@")) // ssh
                {
                    var idx = remoteUrl.IndexOf(':');
                    if (idx > 0 && idx + 1 < remoteUrl.Length)
                    {
                        var path = remoteUrl[(idx + 1)..];
                        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                            path = path[..^4];
                        path = path.TrimEnd('/');
                        return $"https://github.com/{path}";
                    }
                }
                else if (remoteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    var url = remoteUrl;
                    if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                        url = url[..^4];
                    return url.TrimEnd('/');
                }
            }
            return null;
        }
    }
}