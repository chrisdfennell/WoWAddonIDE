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

## 📸 Screenshots

<p align="center">
  <img src="https://github.com/chrisdfennell/WoWAddonIDE/blob/master/WoWAddonIDE/Docs/screenshot-main.png" alt="Main editor" width="900"/><br/>
  <em>Main editor with Lua highlighting, outline, and completion.</em>
</p>

<p align="center">
  <img src="https://github.com/chrisdfennell/WoWAddonIDE/blob/master/WoWAddonIDE/Docs/screenshot-main.png" alt="Help window" width="420"/>
  &nbsp;&nbsp;
  <img src="https://github.com/chrisdfennell/WoWAddonIDE/blob/master/WoWAddonIDE/Docs/screenshot-settings.png" alt="Settings window" width="420"/>
</p>

---

## ✨ Highlights

- **Lua + XML syntax highlighting**
  - Custom `Lua.xshd` and `WoWTOC.xshd` definitions (AvalonEdit).
  - Theme-aware colors; tuned for readability.
- **Autocomplete & parameter hints**
  - Lua keywords, WoW API names (from `wow_api.json`), buffer identifiers, snippets.
  - Signature help on `(` for known functions.
- **Hover docs for WoW API**
  - Tooltips sourced from `Resources/wow_api.json` (extensible/importable).
- **Outline panel (Lua)**
  - Jump to `function`, `local function`, etc.
- **Find in files** (**Ctrl+Shift+F**) and **Go to definition** (**F12**).
- **Project Explorer** + **TOC generator & editor**.
- **Build options**
  - **Build** → copy to your WoW **AddOns** folder (safe copy).
  - **Build to Folder…**, **Build Zip…**.
- **Live reload helper**
  - Writes a `Reload.flag` you can watch to call `ReloadUI()`.
- **Git & GitHub integration**
  - LibGit2Sharp + Octokit workflows (init/clone/commit/push/branches/releases).

> ✅ Designed to be safe by default: the **Build** command will not wipe your source folder or your AddOns folder by mistake.

---

## 🚀 Getting Started

### Prerequisites
- Windows 10/11
- Visual Studio 2022+ (**.NET Desktop** workload)
- .NET 8 SDK
- WoW retail installed (optional, for AddOns path detection)

### Restore packages
The project uses NuGet packages:
- `ICSharpCode.AvalonEdit`
- `Ookii.Dialogs.Wpf`
- `Newtonsoft.Json`
- `DiffPlex`
- `LibGit2Sharp`
- `Octokit`

Build the solution to restore packages automatically.

### Resource files (important)
Ensure these exist with **Build Action = Resource**:
```
/Resources/Lua.xshd
/Resources/WoWTOC.xshd
/Resources/wow_api.json
```
If highlighting fails, check the Output log—loading errors are reported.

### First run
1. **Tools → Settings** → set your WoW **AddOns** folder (auto-detected when possible).
2. **File → New** or **Open** a folder containing your addon's `.toc`.

---

## 🧭 Workflow

1. Create/Open a project folder (where the `.toc` lives).
2. Edit `.lua` / `.xml` files in tabs.
3. Use **Outline** to jump around functions; **Ctrl+Shift+F** to search.
4. **Build** to copy safely into AddOns, or **Build Zip…** to package.
5. Use **Git** menu to commit/push or create a GitHub repo.

---

## 🎨 Theming

- `ThemeManager.ApplyTheme(System|Light|Dark)` – global theme
- `ThemeManager.ApplyToEditor(editor)` – AvalonEdit brushes (background, selection, caret, current line, line numbers)
- XSHD retinting for syntax highlighting

---

## ⌨️ Shortcuts

- **Ctrl+/** — Toggle comment
- **Ctrl+D** — Duplicate line
- **F12** — Go to definition
- **Ctrl+Shift+F** — Find in files
- **Ctrl+S** — Save / **Ctrl+Shift+S** — Save All

---

## 🧰 Troubleshooting

- **Lua highlight null** → ensure `Resources/Lua.xshd` exists, has Build Action = Resource, and valid XML.
- **TOC highlight error** → same checks for `WoWTOC.xshd`.
- **GitHub device flow disabled** → either enable device flow on your OAuth App or use a classic PAT with `repo` scope.
- **Build safety** → source/target identity check prevents destructive copies.

---

## 🗂 Project Structure

```
WoWAddonIDE/
 ├─ Models/
 ├─ Services/
 │   ├─ ThemeManager.cs
 │   ├─ CompletionService.cs
 │   ├─ SymbolService.cs
 │   ├─ GitService.cs
 │   └─ LuaLint.cs
 ├─ Windows/
 ├─ Resources/
 │   ├─ Lua.xshd
 │   ├─ WoWTOC.xshd
 │   └─ wow_api.json
 ├─ Themes/ (optional)
 ├─ MainWindow.xaml / .cs
 └─ README.md
```

---

## 🤝 Contributing

PRs welcome! Ideas:
- Improved Lua grammar & TOC rules
- Smarter symbol index
- Live reload companion addon
- Theme variants & accessibility
- Tests for TOC parser/validator

---

## 📄 License

Licensed under the **MIT License** — see [LICENSE](LICENSE).

Copyright © 2025 Christopher Fennell.

---

## 🙏 Acknowledgements

- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit)
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp)
- [Octokit.NET](https://github.com/octokit/octokit.net)
- [Ookii.Dialogs.Wpf](https://github.com/ookii-dialogs/ookii-dialogs-wpf)
- [DiffPlex](https://github.com/mmanela/diffplex)
