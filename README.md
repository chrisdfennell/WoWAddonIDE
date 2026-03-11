<p align="center">
  <a href="https://github.com/chrisdfennell/WoWAddonIDE/actions/workflows/release.yml">
    <img src="https://github.com/chrisdfennell/WoWAddonIDE/actions/workflows/release.yml/badge.svg" alt="Build & Release" />
  </a>
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

<h1 align="center">WoW Addon IDE</h1>
<p align="center">A lightweight, single-exe IDE for building <b>World of Warcraft</b> addons on Windows.</p>

---

## Screenshots

<p align="center">
  <img src="https://github.com/chrisdfennell/WoWAddonIDE/blob/main/WoWAddonIDE/Docs/screenshot-main.png" alt="Main editor" width="900"/><br/>
  <em>Main editor with Lua highlighting, outline, and completion.</em>
</p>

<p align="center">
  <img src="https://github.com/chrisdfennell/WoWAddonIDE/blob/main/WoWAddonIDE/Docs/screenshot-help.png" alt="Help window" width="420"/>
  &nbsp;&nbsp;
  <img src="https://github.com/chrisdfennell/WoWAddonIDE/blob/main/WoWAddonIDE/Docs/screenshot-settings.png" alt="Settings window" width="420"/>
</p>

---

## Download

Grab the latest **single-exe** from the [Releases](https://github.com/chrisdfennell/WoWAddonIDE/releases/latest) page. No installer required — just download `WoWAddonIDE.exe` and run it.

---

## Highlights

- **Lua + XML + TOC syntax highlighting** — Custom AvalonEdit definitions with theme-aware colors
- **Autocomplete & parameter hints** — Lua keywords, WoW API names, buffer identifiers, and snippets
- **Hover docs for WoW API** — Tooltips sourced from `wow_api.json` (extensible/importable)
- **Outline panel** — Jump to functions, methods (`:` syntax), local tables, and section comments
- **Find in files** (Ctrl+Shift+F) and **Go to definition** (F12)
- **Symbol search** — Project-wide symbol index with incremental updates
- **Project Explorer** + **TOC generator & editor**
- **Build options** — Copy to AddOns folder, build to folder, build zip
- **Live reload helper** — Writes a `Reload.flag` for `ReloadUI()` detection
- **Git & GitHub integration** — Init, clone, commit, push, branches, blame, merge, releases (LibGit2Sharp + Octokit)
- **Diff viewer** — Side-by-side comparison of buffer vs. disk
- **File watcher** — Auto-reload tabs when files change on disk
- **Auto-save** — Configurable timer-based auto-save
- **Recent projects** — MRU list with stale-entry pruning
- **Command palette** — Quick access to IDE commands
- **Color picker** — WoW color code helper with inline previews
- **Minimap** — Code overview sidebar
- **Theming** — System, Light, and Dark themes with full editor retinting
- **Lua linter** — Static analysis for syntax errors, deprecated WoW APIs, trailing whitespace, and unbalanced delimiters
- **Structured logging** — Severity-based logging with file rotation and UI output sink

---

## Getting Started

### Quick start (single exe)
1. Download `WoWAddonIDE.exe` from [Releases](https://github.com/chrisdfennell/WoWAddonIDE/releases/latest)
2. Run it — no install needed
3. **Tools > Settings** — set your WoW AddOns folder
4. **File > New** or **Open** a folder containing your addon's `.toc`

### Build from source

**Prerequisites:**
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022+ (optional, .NET Desktop workload)

```bash
git clone https://github.com/chrisdfennell/WoWAddonIDE.git
cd WoWAddonIDE
dotnet build WoWAddonIDE.sln
dotnet run --project WoWAddonIDE/WoWAddonIDE.csproj
```

**Publish as single exe:**
```bash
dotnet publish WoWAddonIDE/WoWAddonIDE.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Workflow

1. Create/Open a project folder (where the `.toc` lives)
2. Edit `.lua` / `.xml` files in tabs
3. Use **Outline** to jump around functions; **Ctrl+Shift+F** to search
4. **Build** to copy safely into AddOns, or **Build Zip** to package
5. Use **Git** menu to commit/push or create a GitHub release

---

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+S | Save |
| Ctrl+Shift+S | Save All |
| Ctrl+W | Close tab |
| Ctrl+Shift+T | Reopen closed tab |
| Ctrl+/ | Toggle comment |
| Ctrl+D | Duplicate line |
| F12 | Go to definition |
| Ctrl+Shift+F | Find in files |
| Ctrl+P | Command palette |

---

## CI/CD

Every tagged push (`v*`) triggers a GitHub Actions workflow that:
1. Restores dependencies
2. Runs all unit tests
3. Publishes a self-contained single-exe
4. Creates a GitHub Release with the exe attached

```bash
git tag v1.4.0
git push origin v1.4.0
```

---

## Project Structure

```
WoWAddonIDE/
 ├── Models/              # Data models (IDESettings, AddonProject)
 ├── ViewModels/           # MVVM foundation (ViewModelBase, DelegateCommand)
 ├── Services/             # Core services
 │   ├── LogService.cs         # Structured logging with rotation
 │   ├── ThemeManager.cs       # Theme management
 │   ├── CompletionService.cs  # Autocomplete engine
 │   ├── SymbolService.cs      # Project-wide symbol index
 │   ├── OutlineService.cs     # Lua outline/structure parser
 │   ├── GitService.cs         # Git operations (LibGit2Sharp)
 │   ├── LuaLint.cs            # Static analysis & diagnostics
 │   ├── FindInFiles.cs        # Project-wide text search
 │   ├── TocParser.cs          # TOC file generation & parsing
 │   └── SecureStorage.cs      # DPAPI credential storage
 ├── Windows/              # Dialog windows
 ├── Resources/            # Syntax definitions & API data
 │   ├── Lua.xshd
 │   ├── wowtoc.xshd
 │   └── wow_api.json
 ├── Themes/               # WPF theme resource dictionaries
 ├── Constants.cs          # Centralized magic strings & paths
 ├── MainWindow.xaml       # Main UI
 └── MainWindow.*.cs       # Partial classes (Editor, Git, Tabs, etc.)

WoWAddonIDE.Tests/         # xUnit test suite (55 tests)
.github/workflows/        # CI/CD pipeline
```

---

## Testing

```bash
dotnet test WoWAddonIDE.Tests/WoWAddonIDE.Tests.csproj
```

55 tests covering: OutlineService, SymbolService, TocParser, LuaLint, FindInFiles, LogService, and Constants.

---

## Troubleshooting

- **Lua highlight missing** — Ensure `Resources/Lua.xshd` exists with Build Action = Resource
- **TOC highlight error** — Same check for `wowtoc.xshd`
- **GitHub auth issues** — Enable device flow on your OAuth App or use a classic PAT with `repo` scope
- **Build safety** — Source/target identity check prevents destructive copies

---

## Contributing

PRs welcome! Ideas:
- Improved Lua grammar & TOC rules
- Live reload companion addon
- Theme variants & accessibility
- Additional linter rules for WoW-specific patterns

---

## License

Licensed under the **MIT License** — see [LICENSE](LICENSE).

Copyright 2025-2026 Christopher Fennell.

---

## Acknowledgements

- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) — Text editor component
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) — Git integration
- [Octokit.NET](https://github.com/octokit/octokit.net) — GitHub API
- [MoonSharp](https://github.com/moonsharp-devs/moonsharp) — Lua interpreter
- [Ookii.Dialogs.Wpf](https://github.com/ookii-dialogs/ookii-dialogs-wpf) — Native dialogs
- [DiffPlex](https://github.com/mmanela/diffplex) — Diff engine
