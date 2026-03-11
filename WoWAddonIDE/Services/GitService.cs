// File: WoWAddonIDE/Services/GitService.cs
using LibGit2Sharp;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LG = LibGit2Sharp;

namespace WoWAddonIDE.Services
{
    public static class GitService
    {
        // -------- types --------
        public sealed class RepoInfo
        {
            public string? Branch { get; init; }
            public int Ahead { get; init; }
            public int Behind { get; init; }
            public int Added { get; init; }
            public int Deleted { get; init; }
            public int Conflicts { get; init; }
            public bool HasRemote { get; init; }
        }

        public sealed class ConflictTriplet
        {
            public string Path { get; init; } = "";
            public string? BaseText { get; init; }
            public string? OursText { get; init; }
            public string? TheirsText { get; init; }
        }

        public sealed class BlameEntry
        {
            public int StartLine { get; init; }
            public int LineSpan { get; init; }
            public int LineCount { get; init; }
            public string CommitSha { get; init; } = "";
            public string Author { get; init; } = "";
            public DateTimeOffset When { get; init; }
            public string Summary { get; init; } = "";
        }

        // -------- find repo --------
        public static string? FindRepoRoot(string startPath)
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        // -------- credentials helpers --------
        private static LG.Credentials? MakeCreds(Models.IDESettings s)
        {
            if (!string.IsNullOrWhiteSpace(s.GitHubToken))
            {
                return new LG.UsernamePasswordCredentials
                {
                    Username = "x-access-token",
                    Password = s.GitHubToken
                };
            }
            return null;
        }

        private static LG.FetchOptions MakeFetchOptions(Models.IDESettings s)
        {
            var fo = new LG.FetchOptions();
            var c = MakeCreds(s);
            if (c != null) fo.CredentialsProvider = (_url, _user, _types) => c;
            return fo;
        }

        private static LG.PushOptions MakePushOptions(Models.IDESettings s)
        {
            var po = new LG.PushOptions();
            var c = MakeCreds(s);
            if (c != null) po.CredentialsProvider = (_url, _user, _types) => c;
            return po;
        }

        // -------- basic ops --------
        public static void InitRepo(string path)
        {
            if (!LG.Repository.IsValid(path))
                LG.Repository.Init(path);
        }

        // Overload #1 (url, dest)
        public static string Clone(string url, string dest)
        {
            Directory.CreateDirectory(dest);
            var opt = new LG.CloneOptions();
            return LG.Repository.Clone(url, dest, opt);
        }

        // Overload #2 (url, dest, settings)
        public static string Clone(string url, string dest, Models.IDESettings settings)
        {
            Directory.CreateDirectory(dest);
            // Some LibGit2Sharp versions don't allow setting FetchOptions directly on CloneOptions.
            // Do a plain clone; credentials will be used on subsequent fetch/push operations.
            return LG.Repository.Clone(url, dest, new LG.CloneOptions());
        }

        // Overload #3 (url, dest, branch, recurse, settings)
        public static string Clone(string url, string dest, string? branch, bool recurse, Models.IDESettings settings)
        {
            Directory.CreateDirectory(dest);
            var opt = new LG.CloneOptions();
            if (!string.IsNullOrWhiteSpace(branch))
                opt.BranchName = branch;
            // Submodule behaviors vary by version; omit explicit recurse flags for compatibility.
            return LG.Repository.Clone(url, dest, opt);
        }

        // Overload #4 with parameters flipped as seen in some call sites
        public static string Clone(string url, string dest, bool recurse, string? branch, Models.IDESettings settings)
            => Clone(url, dest, branch, recurse, settings);

        public static void EnsureRemote(string repoPath, string name, string url)
        {
            using var repo = new LG.Repository(repoPath);
            var existing = repo.Network.Remotes[name];
            if (existing == null)
                repo.Network.Remotes.Add(name, url);
            else if (!string.Equals(existing.Url, url, StringComparison.OrdinalIgnoreCase))
                repo.Network.Remotes.Update(name, r => r.Url = url);
        }

        public static IEnumerable<string> Status(string repoPath)
        {
            using var repo = new LG.Repository(repoPath);
            var s = repo.RetrieveStatus(new LG.StatusOptions());
            foreach (var e in s)
                yield return $"{e.FilePath}  [{e.State}]";
        }

        public static void CommitAll(string repoPath, Models.IDESettings settings, string message)
        {
            using var repo = new LG.Repository(repoPath);
            LG.Commands.Stage(repo, "*");
            var status = repo.RetrieveStatus(new LG.StatusOptions());
            if (!status.IsDirty) return;

            var sig = MakeSignature(settings);
            repo.Commit(string.IsNullOrWhiteSpace(message) ? "Update" : message, sig, sig);
        }

        private static LG.Signature MakeSignature(Models.IDESettings s)
        {
            var name = string.IsNullOrWhiteSpace(s.GitUserName) ? "WoWAddonIDE" : s.GitUserName;
            var mail = string.IsNullOrWhiteSpace(s.GitUserEmail) ? "noreply@localhost" : s.GitUserEmail;
            return new LG.Signature(name, mail, DateTimeOffset.Now);
        }

        public static void Pull(string repoPath, Models.IDESettings settings)
        {
            using var repo = new LG.Repository(repoPath);
            var sig = MakeSignature(settings);
            var options = new LG.PullOptions
            {
                FetchOptions = MakeFetchOptions(settings),
                MergeOptions = new LG.MergeOptions()
            };
            LG.Commands.Pull(repo, sig, options);
        }

        public static void Push(string repoPath, Models.IDESettings settings, string remoteName = "origin")
        {
            using var repo = new LG.Repository(repoPath);
            var head = repo.Head;
            var refSpec = head?.CanonicalName ?? "refs/heads/main";
            repo.Network.Push(repo.Network.Remotes[remoteName], refSpec, MakePushOptions(settings));
        }

        public static void Sync(string repoPath, Models.IDESettings settings)
        {
            Pull(repoPath, settings);
            Push(repoPath, settings);
        }

        public static List<string> GetBranches(string repoPath)
        {
            using var repo = new LG.Repository(repoPath);
            return repo.Branches.Select(b => b.FriendlyName).ToList();
        }

        public static void CreateBranch(string repoPath, string name, bool checkout)
        {
            using var repo = new LG.Repository(repoPath);
            var b = repo.CreateBranch(name);
            if (checkout) LG.Commands.Checkout(repo, b);
        }

        public static void CheckoutBranch(string repoPath, string name)
        {
            using var repo = new LG.Repository(repoPath);
            var b = repo.Branches[name] ?? throw new InvalidOperationException("Branch not found.");
            LG.Commands.Checkout(repo, b);
        }

        public static string? GetOriginUrl(string repoPath)
        {
            using var repo = new LG.Repository(repoPath);
            return repo.Network.Remotes["origin"]?.Url;
        }

        public static string? ToGitHubWebUrl(string remoteUrl)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl)) return null;
            var u = remoteUrl.Trim();
            if (!u.Contains("github.com", StringComparison.OrdinalIgnoreCase)) return null;
            if (u.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) u = u[..^4];
            return u;
        }

        public static RepoInfo GetRepoInfo(string repoPath)
        {
            using var repo = new LG.Repository(repoPath);
            var head = repo.Head;
            var branch = head?.FriendlyName;

            int ahead = 0, behind = 0;
            var tracked = head?.TrackedBranch;
            if (tracked?.Tip != null && head?.Tip != null)
            {
                var div = repo.ObjectDatabase.CalculateHistoryDivergence(head.Tip, tracked.Tip);
                ahead = div?.AheadBy ?? 0;
                behind = div?.BehindBy ?? 0;
            }

            var status = repo.RetrieveStatus(new LG.StatusOptions());
            int added = status.Count(s =>
                s.State.HasFlag(LG.FileStatus.NewInWorkdir) || s.State.HasFlag(LG.FileStatus.NewInIndex));
            int deleted = status.Count(s =>
                s.State.HasFlag(LG.FileStatus.DeletedFromWorkdir) || s.State.HasFlag(LG.FileStatus.DeletedFromIndex));
            int conflicts = repo.Index.Conflicts.Count();

            return new RepoInfo
            {
                Branch = branch,
                Ahead = ahead,
                Behind = behind,
                Added = added,
                Deleted = deleted,
                Conflicts = conflicts,
                HasRemote = repo.Network.Remotes["origin"] != null
            };
        }

        // -------- history / blame --------
        public static IEnumerable<(string Sha, string Message, DateTimeOffset When, string Author)>
            FileHistory(string repoPath, string relativePath, int max = 200)
        {
            using var repo = new LG.Repository(repoPath);

            // In some versions QueryBy(path) returns LogEntry with a .Commit
            foreach (var le in repo.Commits.QueryBy(relativePath).Take(max))
            {
                var c = le.Commit;
                yield return (c.Sha, c.MessageShort ?? c.Message ?? "", c.Author.When, c.Author.Name);
            }
        }

        public static string GetFileContentAtCommit(string repoPath, string commitSha, string relativePath)
        {
            using var repo = new LG.Repository(repoPath);
            var commit = repo.Lookup<LG.Commit>(commitSha)
                ?? throw new InvalidOperationException("Commit not found");
            var entry = commit[relativePath]
                ?? throw new InvalidOperationException("Path not found in commit");
            var blob = (LG.Blob)entry.Target;
            return blob.GetContentText();
        }

        public static List<BlameEntry> Blame(string repoPath, string relativePath)
        {
            using var repo = new LG.Repository(repoPath);
            var blame = repo.Blame(relativePath, new LG.BlameOptions());
            var result = new List<BlameEntry>();
            foreach (var h in blame)
            {
                var cm = h.FinalCommit;
                result.Add(new BlameEntry
                {
                    StartLine = h.FinalStartLineNumber,
                    LineSpan = h.LineCount,
                    LineCount = h.LineCount,
                    CommitSha = cm.Sha,
                    Author = cm.Author.Name,
                    When = cm.Author.When,
                    Summary = cm.MessageShort ?? cm.Message ?? ""
                });
            }
            return result;
        }

        // -------- conflicts --------
        public static List<ConflictTriplet> GetConflictTriplets(string repoPath)
        {
            using var repo = new LG.Repository(repoPath);
            var list = new List<ConflictTriplet>();

            foreach (var c in repo.Index.Conflicts)
            {
                var relPath = c.Ours?.Path ?? c.Theirs?.Path ?? c.Ancestor?.Path;
                if (relPath == null) continue;

                string? baseText = ReadStageText(repo, c.Ancestor);
                string? oursText = ReadStageText(repo, c.Ours);
                string? theirsText = ReadStageText(repo, c.Theirs);

                list.Add(new ConflictTriplet
                {
                    Path = relPath,
                    BaseText = baseText,
                    OursText = oursText,
                    TheirsText = theirsText
                });
            }
            return list;
        }

        private static string? ReadStageText(LG.Repository repo, LG.IndexEntry? entry)
        {
            if (entry == null) return null;
            var blob = repo.Lookup<LG.Blob>(entry.Id);
            return blob?.GetContentText();
        }

        public static void ResolveConflictWithText(string repoPath, string relativePath, string? mergedText)
        {
            var full = Path.Combine(repoPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, mergedText ?? string.Empty);

            using var repo = new LG.Repository(repoPath);
            LG.Commands.Stage(repo, relativePath);
        }

        // -------- releases --------
        public static async System.Threading.Tasks.Task<string> PublishGitHubReleaseAsync(
            string repoPath,
            string tag,
            string name,
            string body,
            bool prerelease,
            string assetPath,
            Models.IDESettings settings)
        {
            using var repo = new LG.Repository(repoPath);
            var origin = repo.Network.Remotes["origin"]?.Url
                         ?? throw new InvalidOperationException("No 'origin' remote.");

            // parse https://github.com/{owner}/{repo}.git
            string? owner = null, repoName = null;
            var u = origin.TrimEnd('/');
            var ix = u.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase);
            if (ix >= 0)
            {
                var tail = u[(ix + "github.com/".Length)..];
                if (tail.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    tail = tail[..^4];
                var parts = tail.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) { owner = parts[0]; repoName = parts[1]; }
            }
            if (owner == null || repoName == null)
                throw new InvalidOperationException("Origin is not a GitHub https URL.");

            var gh = new GitHubClient(new ProductHeaderValue("WoWAddonIDE"));
            if (!string.IsNullOrWhiteSpace(settings.GitHubToken))
                gh.Credentials = new Octokit.Credentials(settings.GitHubToken);

            Release rel;
            try
            {
                rel = await gh.Repository.Release.Create(owner, repoName,
                    new NewRelease(tag) { Name = name, Body = body, Draft = false, Prerelease = prerelease });
            }
            catch (ApiValidationException)
            {
                var rels = await gh.Repository.Release.GetAll(owner, repoName);
                rel = rels.First(r => r.TagName == tag);
            }

            await using var fs = File.OpenRead(assetPath);
            var upload = new ReleaseAssetUpload(Path.GetFileName(assetPath), "application/octet-stream", fs, null);
            await gh.Repository.Release.UploadAsset(rel, upload);
            return rel.HtmlUrl;
        }

        // Overload to match call site: PublishGitHubReleaseAsync(repoPath, _settings, zipPath, tag, name, body, prerelease)
        public static async System.Threading.Tasks.Task<string> PublishGitHubReleaseAsync(
            string repoPath,
            Models.IDESettings settings,
            string assetPath,
            string tag,
            string name,
            string body,
            bool prerelease)
        {
            return await PublishGitHubReleaseAsync(
                repoPath,
                tag,
                name,
                body,
                prerelease,
                assetPath,
                settings);
        }
    }
}