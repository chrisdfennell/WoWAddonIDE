using LibGit2Sharp;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LG = LibGit2Sharp;        // <— single alias



namespace WoWAddonIDE.Services
{
    public static class GitService
    {
        // ------------------------- TYPES -------------------------

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
            public string CommitSha { get; init; } = "";
            public string Author { get; init; } = "";
            public DateTimeOffset When { get; init; }
            public string Summary { get; init; } = "";
            public int LineCount => LineSpan;
        }

        public static string? FindRepoRoot(string startPath)
        {
            if (string.IsNullOrWhiteSpace(startPath)) return null;
            var dir = new DirectoryInfo(Path.GetFullPath(startPath));
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }


        // ---------------------- CREDENTIALS ----------------------

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
            if (c != null)
                fo.CredentialsProvider = (_url, _user, _types) => c;
            return fo;
        }

        private static LG.PushOptions MakePushOptions(Models.IDESettings s)
        {
            var po = new LG.PushOptions();
            var c = MakeCreds(s);
            if (c != null)
                po.CredentialsProvider = (_url, _user, _types) => c;
            return po;
        }

        // ---------------------- BASIC OPS ------------------------

        public static void InitRepo(string path)
        {
            if (!LG.Repository.IsValid(path))
                LG.Repository.Init(path);
        }

        public static string Clone(string url, string dest, WoWAddonIDE.Models.IDESettings settings)
        {
            var opt = new LG.CloneOptions(); // LG = LibGit2Sharp

            if (!string.IsNullOrWhiteSpace(settings.GitHubToken))
            {
                opt.FetchOptions.CredentialsProvider = (_url, _user, _types) =>
                    new LG.UsernamePasswordCredentials
                    {
                        Username = settings.GitHubToken,
                        Password = "x-oauth-basic"
                    };
            }

            return LG.Repository.Clone(url, dest, opt);
        }

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
            var s = repo.RetrieveStatus();
            foreach (var e in s)
                yield return $"{e.FilePath}  [{e.State}]";
        }

        public static void CommitAll(string repoPath, Models.IDESettings settings, string message)
        {
            using var repo = new LG.Repository(repoPath);

            Commands.Stage(repo, "*");

            var status = repo.RetrieveStatus(new LG.StatusOptions());
            if (!status.IsDirty) return;

            var sig = MakeSignature(settings);
            repo.Commit(string.IsNullOrWhiteSpace(message) ? "Update" : message, sig, sig);
        }

        private static LG.Signature MakeSignature(Models.IDESettings s)
        {
            var name = string.IsNullOrWhiteSpace(s.GitUserName) ? "WoWAddonIDE" : s.GitUserName;
            var mail = string.IsNullOrWhiteSpace(s.GitUserEmail) ? "wowaddonide@local" : s.GitUserEmail;
            return new LG.Signature(name, mail, DateTimeOffset.Now);
        }

        public static void Pull(string repoPath, Models.IDESettings settings)
        {
            using var repo = new LG.Repository(repoPath);
            var sig = MakeSignature(settings);

            var opts = new LG.PullOptions
            {
                FetchOptions = MakeFetchOptions(settings)
                // Don’t set MergeOptions.FileConflictStrategy — older LibGit2Sharp may not have it.
            };

            Commands.Pull(repo, sig, opts);
        }

        public static void Push(string repoPath, Models.IDESettings settings, string remote = "origin", string? branch = null)
        {
            using var repo = new LG.Repository(repoPath);
            var current = repo.Head;
            var branchName = branch ?? current.FriendlyName;

            // Ensure refs/heads/ prefix
            var refSpec = branchName.StartsWith("refs/heads/", StringComparison.OrdinalIgnoreCase)
                ? branchName
                : $"refs/heads/{branchName}";

            repo.Network.Push(repo.Network.Remotes[remote], refSpec, MakePushOptions(settings));
        }

        public static void Sync(string repoPath, Models.IDESettings settings)
        {
            Pull(repoPath, settings);
            Push(repoPath, settings);
        }

        public static List<ConflictTriplet> GetConflictTriplets(string repoPath)
        {
            using var repo = new LG.Repository(repoPath);
            var list = new List<ConflictTriplet>();

            foreach (var c in repo.Index.Conflicts)
            {
                var rel = c.Ours?.Path ?? c.Theirs?.Path ?? c.Ancestor?.Path ?? "";
                if (string.IsNullOrEmpty(rel)) continue;

                list.Add(new ConflictTriplet
                {
                    Path = rel,
                    BaseText = ReadStageText(repo, rel, LG.StageLevel.Ancestor),
                    OursText = ReadStageText(repo, rel, LG.StageLevel.Ours),
                    TheirsText = ReadStageText(repo, rel, LG.StageLevel.Theirs)
                });
            }
            return list;
        }

        public static IEnumerable<string> GetBranches(string repoPath)
        {
            using var repo = new LG.Repository(repoPath);
            return repo.Branches.Select(b => b.FriendlyName).ToList();
        }

        public static void CreateBranch(string repoPath, string name, bool checkout)
        {
            using var repo = new LG.Repository(repoPath);
            var b = repo.CreateBranch(name);
            if (checkout)
                Commands.Checkout(repo, b);
        }

        public static void CheckoutBranch(string repoPath, string name)
        {
            using var repo = new LG.Repository(repoPath);
            var b = repo.Branches[name] ?? throw new InvalidOperationException("Branch not found.");
            Commands.Checkout(repo, b);
        }

        public static string? GetOriginUrl(string repoPath)
        {
            using var repo = new LG.Repository(repoPath);
            return repo.Network.Remotes["origin"]?.Url;
        }

        public static string? ToGitHubWebUrl(string remoteUrl)
        {
            if (string.IsNullOrWhiteSpace(remoteUrl)) return null;

            try
            {
                string core = remoteUrl.Trim();

                if (core.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
                {
                    core = core.Substring("git@github.com:".Length);
                    core = core.TrimEnd('/', '\\');
                    if (core.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                        core = core.Substring(0, core.Length - 4);
                    return $"https://github.com/{core}";
                }

                if (core.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
                {
                    core = core.TrimEnd('/', '\\');
                    if (core.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                        core = core.Substring(0, core.Length - 4);
                    return core;
                }
            }
            catch { /* ignore */ }

            return null;
        }

        // ----------------------- REPO INFO ------------------------

        public static RepoInfo GetRepoInfo(string repoPath)
        {
            using var repo = new LG.Repository(repoPath);
            var head = repo.Head;
            var branch = head?.FriendlyName;

            int ahead = 0, behind = 0;
            var tracked = head?.TrackedBranch; // not Upstream
            if (tracked?.Tip != null && head?.Tip != null)
            {
                var div = repo.ObjectDatabase.CalculateHistoryDivergence(head.Tip, tracked.Tip);
                ahead = div?.AheadBy ?? 0;
                behind = div?.BehindBy ?? 0;
            }

            var status = repo.RetrieveStatus(new LG.StatusOptions());
            int added = status.Count(s => s.State.HasFlag(LG.FileStatus.NewInWorkdir) || s.State.HasFlag(LG.FileStatus.NewInIndex));
            int deleted = status.Count(s => s.State.HasFlag(LG.FileStatus.DeletedFromWorkdir) || s.State.HasFlag(LG.FileStatus.DeletedFromIndex));
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

        // ------------------ HISTORY / CONTENT ---------------------

        public static IEnumerable<(string Sha, string Message, DateTimeOffset When, string Author)>
            FileHistory(string repoPath, string relativePath)
        {
            using var repo = new LG.Repository(repoPath);
            foreach (var h in repo.Commits.QueryBy(relativePath))
            {
                var cm = h.Commit;
                yield return (cm.Sha, cm.MessageShort ?? cm.Message ?? "", cm.Author.When, cm.Author.Name);
            }
        }

        public static string GetFileContentAtCommit(string repoPath, string commitSha, string relativePath)
        {
            using var repo = new LG.Repository(repoPath);
            var commit = repo.Lookup<LG.Commit>(commitSha)
                ?? throw new InvalidOperationException("Commit not found");

            var entry = commit.Tree[relativePath]
                ?? throw new InvalidOperationException("Path not found in commit");

            var blob = (LG.Blob)entry.Target;
            return blob.GetContentText();
        }

        public static List<BlameEntry> Blame(string repoPath, string relativePath)
        {
            using var repo = new LG.Repository(repoPath);
            var blame = repo.Blame(relativePath, new LG.BlameOptions());
            var result = new List<BlameEntry>();

            foreach (var h in blame) // BlameHunkCollection is enumerable
            {
                var cm = h.FinalCommit; // use FinalCommit for builds lacking FinalCommitId
                result.Add(new BlameEntry
                {
                    StartLine = h.FinalStartLineNumber,
                    LineSpan = h.LineCount,
                    CommitSha = cm?.Sha ?? "",
                    Author = cm?.Author?.Name ?? "",
                    When = cm?.Author.When ?? default,
                    Summary = cm?.MessageShort ?? cm?.Message ?? ""
                });
            }
            return result;
        }


        // ---------------------- CONFLICTS -------------------------

        public static void ResolveConflictWithText(string repoPath, string relativePath, string mergedText)
        {
            var full = Path.Combine(repoPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, mergedText ?? string.Empty);

            using var repo = new LG.Repository(repoPath);
            Commands.Stage(repo, relativePath);  // clears the conflict entry for that path
        }

        private static string ReadStageText(LG.Repository repo, string relPath, LG.StageLevel stage)
        {
            var c = repo.Index.Conflicts
                .FirstOrDefault(x =>
                       string.Equals(x.Ours?.Path, relPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Theirs?.Path, relPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Ancestor?.Path, relPath, StringComparison.OrdinalIgnoreCase));

            if (c == null) return string.Empty;

            LG.ObjectId? id = stage switch
            {
                LG.StageLevel.Ours => c.Ours?.Id,
                LG.StageLevel.Theirs => c.Theirs?.Id,
                LG.StageLevel.Ancestor => c.Ancestor?.Id,
                _ => null
            };

            if (id is null) return string.Empty;

            var blob = repo.Lookup<LG.Blob>(id);
            return blob?.GetContentText() ?? string.Empty;
        }

        // ------------------- GITHUB RELEASES ----------------------

        // GitService.cs
        public static async System.Threading.Tasks.Task<string> PublishGitHubReleaseAsync(
            string repoPath,
            WoWAddonIDE.Models.IDESettings settings,
            string assetPath,
            string tag,
            string name,
            string body,
            bool prerelease)                   // 7th arg expected by your call site
        {
            if (string.IsNullOrWhiteSpace(settings.GitHubToken))
                throw new InvalidOperationException("GitHub token is empty. Set it in Git/GitHub Settings.");

            using var repo = new LG.Repository(repoPath);
            var origin = repo.Network.Remotes["origin"]?.Url
                         ?? throw new InvalidOperationException("No 'origin' remote.");

            // parse "https://github.com/{owner}/{repo}.git"
            string? owner = null, repoName = null;
            var u = origin.TrimEnd('/');
            var ix = u.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase);
            if (ix >= 0)
            {
                var tail = u[(ix + "github.com/".Length)..].TrimEnd(".git".ToCharArray());
                var parts = tail.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2) { owner = parts[0]; repoName = parts[1]; }
            }
            if (owner == null || repoName == null)
                throw new InvalidOperationException("Origin is not a GitHub https URL.");

            var gh = new Octokit.GitHubClient(new ProductHeaderValue("WoWAddonIDE"))
            { Credentials = new Octokit.Credentials(settings.GitHubToken) };

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
            var upload = new ReleaseAssetUpload(Path.GetFileName(assetPath), "application/zip", fs, null);
            await gh.Repository.Release.UploadAsset(rel, upload);
            return rel.HtmlUrl;
        }
    }
}