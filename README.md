<p align="center">
  <a href="https://github.com/chrisdfennell/WoWAddonIDE/releases/latest">
    <img src="https://img.shields.io/github/v/release/chrisdfennell/WoWAddonIDE?display_name=tag&sort=semver" alt="Latest Release" />
  </a>
  <a href="https://github.com/chrisdfennell/WoWAddonIDE/releases/latest">
    <img src="https://img.shields.io/github/release-date/chrisdfennell/WoWAddonIDE" alt="Release Date" />
  </a>
  <a href="https://github.com/chrisdfennell/WoWAddonIDE/releases">
    <img src="https://img.shields.io/github/downloads/chrisdfennell/WoWAddonIDE/total" alt="Total Downloads" />
  </a>
  <a href="https://github.com/chrisdfennell/WoWAddonIDE/commits/main">
    <img src="https://img.shields.io/github/last-commit/chrisdfennell/WoWAddonIDE" alt="Last Commit" />
  </a>
  <a href="https://github.com/chrisdfennell/WoWAddonIDE/issues">
    <img src="https://img.shields.io/github/issues/chrisdfennell/WoWAddonIDE" alt="Open Issues" />
  </a>
  <a href="https://github.com/chrisdfennell/WoWAddonIDE/pulls">
    <img src="https://img.shields.io/github/issues-pr/chrisdfennell/WoWAddonIDE" alt="Open PRs" />
  </a>
  <a href="https://github.com/chrisdfennell/WoWAddonIDE/stargazers">
    <img src="https://img.shields.io/github/stars/chrisdfennell/WoWAddonIDE" alt="GitHub Stars" />
  </a>
  <a href="https://dotnet.microsoft.com/">
    <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8" />
  </a>
  <a href="https://learn.microsoft.com/dotnet/desktop/wpf/">
    <img src="https://img.shields.io/badge/WPF-Desktop-0078D6?logo=windows&logoColor=white" alt="WPF" />
  </a>
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white" alt="Windows" />
  <a href="LICENSE">
    <img src="https://img.shields.io/badge/License-MIT-green" alt="License: MIT" />
  </a>
  <a href="https://github.com/icsharpcode/AvalonEdit">
    <img src="https://img.shields.io/badge/Editor-AvalonEdit-4B32C3" alt="AvalonEdit" />
  </a>
  <a href="https://github.com/libgit2/libgit2sharp">
    <img src="https://img.shields.io/badge/Git-LibGit2Sharp-1F6FEB?logo=git&logoColor=white" alt="LibGit2Sharp" />
  </a>
  <a href="https://github.com/octokit/octokit.net">
    <img src="https://img.shields.io/badge/GitHub-Octokit-181717?logo=github&logoColor=white" alt="Octokit.NET" />
  </a>
  <img src="https://img.shields.io/badge/PRs-welcome-brightgreen" alt="PRs welcome" />
</p>

<h1 align="center">WoW Addon IDE (WPF)</h1>
<p align="center">A lightweight IDE for building <b>World of Warcraft</b> addons on Windows.</p>

---

## ✨ What’s new (OAuth & packaging)

- **Sign in with GitHub** via browser using **Authorization Code + PKCE** (no client secret required at runtime).
- **Pick → Clone → Open** a repo directly from your GitHub user/orgs, or **Initialize** the current project to GitHub.
- **Ship builds without secrets**: all credentials are **User-scoped** in `Properties/Settings.settings` and are not embedded in the EXE.

---

## 🚀 Quick Start

### Prerequisites
- Windows 10/11
- Visual Studio 2022+ (with **.NET Desktop** workload) or `dotnet SDK` **8.0+**
- Git

### Restore packages
Build once to restore NuGet:
- `ICSharpCode.AvalonEdit`
- `Ookii.Dialogs.Wpf`
- `LibGit2Sharp`
- `Octokit`
- `DiffPlex`
- `Newtonsoft.Json`

### First run
1. **Tools → Settings** → set your WoW **AddOns** folder.
2. **Git → Sign in with GitHub** (browser opens; passkeys/2FA supported).
3. **Git → GitHub…** to clone/open/init repos.

---

## 🔐 GitHub OAuth without a Secret

This app uses **PKCE + loopback** so you can ship with **only a Client ID**.

### 1) Create an OAuth App (GitHub)
- Go to <https://github.com/settings/developers> → **New OAuth App**
- **Homepage URL:** `http://127.0.0.1`
- **Authorization callback URL:** `http://127.0.0.1:53682/callback`  
  *(You can change the port in settings; see below.)*
- Copy the **Client ID**. **Client Secret is optional** when using PKCE.

### 2) Configure `Properties/Settings.settings` (User-scoped)
| Key                   | Type   | Scope | Default               | Notes                                   |
|-----------------------|--------|-------|-----------------------|-----------------------------------------|
| `GitHubClientId`      | string | User  | _(empty)_             | Required for OAuth                      |
| `GitHubClientSecret`  | string | User  | _(empty)_             | **Optional** with PKCE                  |
| `GitHubOAuthPort`     | int    | User  | `53682`               | Loopback port for redirect              |
| `GitHubScopes`        | string | User  | `repo read:user`      | Adjust to taste                         |

User-scoped settings are stored in `%LOCALAPPDATA%\\...user.config` and **are not** embedded in the build.

### 3) Supplying values (choose any)
- **Settings UI** inside the app (recommended)
- **Environment variables** (handy for CI / dev machines):
  - `WOWIDE_GITHUB_CLIENT_ID`
  - `WOWIDE_GITHUB_CLIENT_SECRET`
- **Optional file:** `%APPDATA%\\WoWAddonIDE\\secrets.json` (gitignored)
  ```json
  {
    "clientId": "YOUR_CLIENT_ID",
    "clientSecret": ""
  }
  ```

Add this to your `.gitignore`:
```
# app/runtime secrets
**/secrets.json
```

> If you choose to use a Client Secret (not required with PKCE), **do not** commit it. Keep it in env vars or `secrets.json`.

---

## 🏗️ Build & Publish

### Visual Studio
- Open `WoWAddonIDE.sln`
- Set **Release**
- Build / Publish (ClickOnce, MSIX, or folder)

### CLI (folder publish)
```powershell
dotnet publish WoWAddonIDE/WoWAddonIDE.csproj -c Release -r win-x64 --self-contained false
```

The app reads settings at runtime; no secrets are baked into the binaries.

---

## 🧭 Using the Git/GitHub features

- **Git → Sign in with GitHub**  
  Browser pops; approve the app. The token is stored in **User settings**.
- **Git → GitHub…**  
  - **Clone** a repo from your user/orgs into a chosen folder (the app waits for your `.toc`)
  - **Open** an existing local project
  - **Initialize** the current project on GitHub (creates repo, sets `origin`, initial commit + push)
- **Status / Commit / Pull / Push / Sync** and **Branches** supported from the Git menu.
- **History / Blame / Merge Helper** in dedicated windows.

---

## 🗂️ Project Structure

```
WoWAddonIDE/
 ├─ Models/
 ├─ Services/
 │   ├─ GitService.cs
 │   ├─ GitHubAuthService.cs
 │   ├─ CompletionService.cs
 │   ├─ SymbolService.cs
 │   └─ ThemeManager.cs
 ├─ Windows/
 ├─ Resources/
 │   ├─ Lua.xshd
 │   ├─ WoWTOC.xshd
 │   └─ wow_api.json
 ├─ MainWindow.xaml / .cs
 └─ README.md
```

---

## ❓ Troubleshooting

- **“Watching for .toc under …\\.git”**  
  Newer builds normalize clone/open paths to the **working directory**, not `.git`.
- **“Resource not accessible by PAT”**  
  Prefer OAuth; or use a classic PAT with `repo` scope. If your org enforces SAML, authorize the token for that org.
- **Syntax highlighting missing**  
  Ensure `Resources/*.xshd` and `wow_api.json` exist with Build Action = **Resource**.

---

## 🤝 Contributing & License

PRs welcome! MIT license—see [LICENSE](LICENSE).
