```{=html}
<p align="center">
```
`<a href="https://github.com/chrisdfennell/WoWAddonIDE/releases/latest">`{=html}
`<img src="https://img.shields.io/github/v/release/chrisdfennell/WoWAddonIDE?display_name=tag&sort=semver" alt="Latest Release" />`{=html}
`</a>`{=html}
`<a href="https://github.com/chrisdfennell/WoWAddonIDE/releases/latest">`{=html}
`<img src="https://img.shields.io/github/release-date/chrisdfennell/WoWAddonIDE" alt="Release Date" />`{=html}
`</a>`{=html}
`<a href="https://github.com/chrisdfennell/WoWAddonIDE/releases">`{=html}
`<img src="https://img.shields.io/github/downloads/chrisdfennell/WoWAddonIDE/total" alt="Total Downloads" />`{=html}
`</a>`{=html}
`<a href="https://github.com/chrisdfennell/WoWAddonIDE/commits/main">`{=html}
`<img src="https://img.shields.io/github/last-commit/chrisdfennell/WoWAddonIDE" alt="Last Commit" />`{=html}
`</a>`{=html}
`<a href="https://github.com/chrisdfennell/WoWAddonIDE/issues">`{=html}
`<img src="https://img.shields.io/github/issues/chrisdfennell/WoWAddonIDE" alt="Open Issues" />`{=html}
`</a>`{=html}
`<a href="https://github.com/chrisdfennell/WoWAddonIDE/pulls">`{=html}
`<img src="https://img.shields.io/github/issues-pr/chrisdfennell/WoWAddonIDE" alt="Open PRs" />`{=html}
`</a>`{=html}
`<a href="https://github.com/chrisdfennell/WoWAddonIDE/stargazers">`{=html}
`<img src="https://img.shields.io/github/stars/chrisdfennell/WoWAddonIDE" alt="GitHub Stars" />`{=html}
`</a>`{=html} `<a href="https://dotnet.microsoft.com/">`{=html}
`<img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8" />`{=html}
`</a>`{=html}
`<a href="https://learn.microsoft.com/dotnet/desktop/wpf/">`{=html}
`<img src="https://img.shields.io/badge/WPF-Desktop-0078D6?logo=windows&logoColor=white" alt="WPF" />`{=html}
`</a>`{=html}
`<img src="https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white" alt="Windows" />`{=html}
`<a href="LICENSE">`{=html}
`<img src="https://img.shields.io/badge/License-MIT-green" alt="License: MIT" />`{=html}
`</a>`{=html}
`<a href="https://github.com/icsharpcode/AvalonEdit">`{=html}
`<img src="https://img.shields.io/badge/Editor-AvalonEdit-4B32C3" alt="AvalonEdit" />`{=html}
`</a>`{=html}
`<a href="https://github.com/libgit2/libgit2sharp">`{=html}
`<img src="https://img.shields.io/badge/Git-LibGit2Sharp-1F6FEB?logo=git&logoColor=white" alt="LibGit2Sharp" />`{=html}
`</a>`{=html} `<a href="https://github.com/octokit/octokit.net">`{=html}
`<img src="https://img.shields.io/badge/GitHub-Octokit-181717?logo=github&logoColor=white" alt="Octokit.NET" />`{=html}
`</a>`{=html}
`<img src="https://img.shields.io/badge/PRs-welcome-brightgreen" alt="PRs welcome" />`{=html}
```{=html}
</p>
```
```{=html}
<h1 align="center">
```
WoW Addon IDE (WPF)
```{=html}
</h1>
```
```{=html}
<p align="center">
```
A lightweight IDE for building `<b>`{=html}World of
Warcraft`</b>`{=html} addons on Windows.
```{=html}
</p>
```

------------------------------------------------------------------------

## 📸 Screenshots

```{=html}
<p align="center">
```
`<img src="https://github.com/chrisdfennell/WoWAddonIDE/blob/master/WoWAddonIDE/Docs/screenshot-main.png" alt="Main editor" width="900"/>`{=html}`<br/>`{=html}
`<em>`{=html}Main editor with Lua highlighting, outline, and
completion.`</em>`{=html}
```{=html}
</p>
```
```{=html}
<p align="center">
```
`<img src="https://github.com/chrisdfennell/WoWAddonIDE/blob/master/WoWAddonIDE/Docs/screenshot-main.png" alt="Help window" width="420"/>`{=html}
  
`<img src="https://github.com/chrisdfennell/WoWAddonIDE/blob/master/WoWAddonIDE/Docs/screenshot-settings.png" alt="Settings window" width="420"/>`{=html}
```{=html}
</p>
```

------------------------------------------------------------------------

## ✨ Highlights

-   **Lua + XML syntax highlighting**
    -   Custom `Lua.xshd` and `WoWTOC.xshd` (AvalonEdit), theme-aware
        colors.
-   **Autocomplete & parameter hints**
    -   Lua keywords, WoW API names (from `wow_api.json`), snippets,
        buffer identifiers; signature help on `(`.
-   **Hover docs for WoW API**
    -   Tooltips from `Resources/wow_api.json` (extensible/importable).
-   **Outline panel (Lua)**
    -   Jump to `function`, `local function`, etc.
-   **Find in files** (**Ctrl+Shift+F**) and **Go to definition**
    (**F12**).
-   **Project Explorer** + **TOC generator & editor**.
-   **Build options**
    -   **Build** → safe copy to your WoW **AddOns** folder.
    -   **Build to Folder...**, **Build Zip...**.
-   **Live reload helper**
    -   Writes a `Reload.flag` you can watch to call `ReloadUI()`.
-   **Git & GitHub integration**
    -   LibGit2Sharp + Octokit workflows
        (init/clone/commit/push/branches/releases).

> ✅ Designed to be safe by default: **Build** will not wipe your source
> or AddOns folder.

------------------------------------------------------------------------

## 🔐 What's New (OAuth & Packaging)

-   **Sign in with GitHub** via browser using **Authorization Code +
    PKCE** (no runtime client secret).
-   **Pick → Clone → Open** repos from your GitHub user/orgs, or
    **Initialize** the current project on GitHub.
-   **No secrets in binaries**: credentials are **User-scoped** in
    `Properties/Settings.settings` (stored in `%LOCALAPPDATA%`).

### GitHub OAuth without a Secret (PKCE + Loopback)

1.  **Create an OAuth App**

    -   <https://github.com/settings/developers> → **New OAuth App**
    -   **Homepage URL:** `http://127.0.0.1`
    -   **Authorization callback URL:**
        `http://127.0.0.1:53682/callback`\
        *(Port is configurable in settings.)*
    -   Copy **Client ID** (secret is **optional** with PKCE).

2.  **User-Scoped Settings** (`Properties/Settings.settings`) \| Key \|
    Type \| Scope \| Default \|
    \|----------------------\|--------\|-------\|------------------\| \|
    `GitHubClientId` \| string \| User \| *(empty)* \| \|
    `GitHubClientSecret` \| string \| User \| *(empty)* \| \|
    `GitHubOAuthPort` \| int \| User \| `53682` \| \| `GitHubScopes` \|
    string \| User \| `repo read:user` \|

3.  **Supplying Values**

    -   In-app Settings UI (recommended), or

    -   **Environment variables:** `WOWIDE_GITHUB_CLIENT_ID`,
        `WOWIDE_GITHUB_CLIENT_SECRET`, or

    -   **Optional file (gitignored):**
        `%APPDATA%\WoWAddonIDE\secrets.json`

        ``` json
        {
          "clientId": "YOUR_CLIENT_ID",
          "clientSecret": ""
        }
        ```

*Add this to `.gitignore`:*

    # app/runtime secrets
    **/secrets.json
