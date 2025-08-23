<p align="center">
  <a href="https://dotnet.microsoft.com/">
    <img src="https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white" alt=".NET 8" />
  </a>
  <a href="https://learn.microsoft.com/dotnet/desktop/wpf/">
    <img src="https://img.shields.io/badge/WPF-Desktop-0078D6?logo=windows&logoColor=white" alt="WPF" />
  </a>
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows&logoColor=white" alt="Windows" />
  <img src="https://img.shields.io/badge/License-TBD-lightgray" alt="License TBD" />
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

# WoW Addon IDE (WPF)

A lightweight IDE for building **World of Warcraft** addons on Windows.  
Built with **WPF + AvalonEdit**, it includes Lua/XML syntax highlighting, IntelliSense-style completion, WoW API hover docs, a project explorer, safe build/publish helpers, and Git/GitHub integration.

> ✅ Designed to be safe by default: the **Build** command will not wipe your source folder or your AddOns folder by mistake.

---

## Highlights

- **Lua + XML syntax highlighting**
  - Custom `Lua.xshd` and `WoWTOC.xshd` (TOC) definitions (AvalonEdit).
  - Theme-aware colors (retinted for light/dark automatically).

- **Autocomplete & parameter hints**
  - Completion for Lua keywords, WoW API names, globals, and snippets.
  - Signature help on `(` with parameter tooltips.

- **Hover docs for WoW API**
  - Tooltips sourced from `Resources/wow_api.json`.

- **Outline panel (Lua)**
  - Quick jump to `function name(...)` and `local name = function(...)`.

- **Find in files (Ctrl+Shift+F)** and **Go to definition (F12)**
  - Simple symbol index across your project; jumps to file/line.

- **Project Explorer** with TOC generator & editor
  - Generate default `.toc`, validate file references and required keys.
  - GUI TOC editor for `Interface`, `Title`, `Notes`, and file list.

- **Build options**
  - **Build** → copy to your WoW **AddOns** folder (safe copy).
  - **Build to Folder…** → copy to any folder.
  - **Build Zip…** → make a distributable zip (DiffPlex for diff viewer).

- **Live reload helper**
  - Writes a `Reload.flag` file your addon can watch and call `ReloadUI()`.

- **Git & GitHub integration**
  - Init/Clone/Status/Commit/Pull/Push/Sync, branches, open on GitHub.
  - Create GitHub repo (PAT), or OAuth sign-in (device code flow) if enabled.
  - Uses **LibGit2Sharp** and **Octokit**.

- **Light/Dark/System themes**
  - Global theme engine + retinting for editors/XSHD.
  - Manual choice persists; otherwise follows OS setting.

---

## Getting Started

### Prerequisites

- Windows 10/11
- Visual Studio 2022+ (with **.NET Desktop** workload)
- .NET 7 or 8 SDK (project targets WPF)
- WoW retail installed (optional, for Build to AddOns convenience)

### Restore packages

The project uses NuGet for:

- `ICSharpCode.AvalonEdit`
- `Ookii.Dialogs.Wpf`
- `Newtonsoft.Json`
- `DiffPlex`
- `LibGit2Sharp`
- `Octokit`

Open the solution and **Build** to restore packages automatically.

### Resource files (important)

These files must be added with **Build Action = Resource**:

```
/Resources/Lua.xshd
/Resources/WoWTOC.xshd
/Resources/wow_api.json
/Themes/Base.xaml
/Themes/Light.xaml
/Themes/Dark.xaml
```

> If the highlight isn’t applied, check the **Output** panel: the app logs when it can’t find or load an XSHD (invalid XML, wrong build action/path, etc.).

### First run

1. **Tools → Settings**  
   Set your WoW **AddOns** folder (detected automatically for standard installs).
2. **File → New** or **File → Open** a folder containing your addon's `.toc`.

You’re good to code!

---

## Typical Workflow

1. **Create/Open** a project folder (where the `.toc` lives).
2. Edit `.lua` / `.xml` files in tabs.
3. Use **Outline** to jump around functions, **Ctrl+Shift+F** to search.
4. **Build** to copy safely into your AddOns folder, or **Build Zip…** to package.
5. Use **Git** menu to commit/push or create a GitHub repo.

---

## Git & GitHub

### Git identity
- Set your identity in **Git/GitHub Settings…** (`GitUserName`, `GitUserEmail`).

### Remotes
- **Git → Set Remote (origin)…** to add an HTTPS remote.

### PAT vs OAuth
- **Create GitHub Repo…** requires permissions:
  - Easiest is a **classic PAT** with `repo` scope, pasted into **Git/GitHub Settings…**.
  - OAuth device flow can be used if the **GitHub OAuth App** for this IDE has **Device Flow** enabled. The app opens a code and URL; sign in with passkeys/2FA in your browser.

> If you see “Resource not accessible by personal access token”, your token lacks scopes or SAML SSO isn’t authorized for your org. You can always create the repo on GitHub and then **Set Remote** + **Push**.

---

## Build Safety

To prevent accidentally nuking your source or live AddOns folder:

- The IDE computes **absolute source and target paths** and refuses to build when they’re identical.
- If your project already lives **inside** AddOns, **Build** is disabled by default (toggleable in Settings).

---

## Theming

Theme files:
```
/Themes/Base.xaml      # shared styles
/Themes/Light.xaml     # light palette (Brush.* keys)
/Themes/Dark.xaml      # dark palette (Brush.* keys)
```

Runtime engine:
- `ThemeManager.Initialize(settings)` – loads theme and sets up OS monitoring.
- `ThemeManager.ApplyTheme(System|Light|Dark)` – applies and persists.
- `ThemeManager.ApplyToEditor(editor)` – sets AvalonEdit brushes (background, foreground, current line border, selection, and line numbers).

If you add new colors (e.g., `Brush.EditorCurrentLineBG`), `ApplyToEditor` will pick them up automatically.

---

## Keyboard Shortcuts

- **Ctrl+/** — Toggle line comment
- **Ctrl+D** — Duplicate line
- **F12** — Go to definition (project symbol index)
- **Ctrl+Shift+F** — Find in files
- **Ctrl+T** — Workspace symbol search
- **Ctrl+S** — Save
- **Ctrl+Shift+S** — Save All

---

## Troubleshooting

**“Lua highlight def is NULL”**  
The IDE couldn’t load `Lua.xshd`. Check:
- The file is at `Resources/Lua.xshd` (case-sensitive in pack URIs).
- **Build Action = Resource**.
- XSHD XML is valid. The Output panel shows the first XML error line/column.

**TOC highlight error**  
Same checks as above for `Resources/WoWTOC.xshd`. Regex support in XSHD is limited—use `<Rule ... />` or `<Keywords/>`/`<Span/>` as supported by AvalonEdit.

**GitHub 400 device_flow_disabled**  
Your OAuth App must explicitly enable **Device Flow**. Alternatively use a **classic PAT** with `repo` scope.

**Build cleared my folder?**  
It shouldn’t. The IDE only cleans the **destination** when it’s different from the source. If you changed settings manually to allow builds inside AddOns, be careful.

---

## Project Structure

```
WoWAddonIDE/
 ├─ Models/
 ├─ Services/
 │   ├─ ThemeManager.cs          # runtime theme engine
 │   ├─ CompletionService.cs     # autocomplete + parameter hints
 │   ├─ SymbolService.cs         # project symbol index
 │   ├─ GitService.cs            # LibGit2Sharp glue
 │   └─ LuaLint.cs               # simple inline diagnostics
 ├─ Windows/                     # dialogs (Find, Diff, TOC, Git, etc.)
 ├─ Resources/
 │   ├─ Lua.xshd
 │   ├─ WoWTOC.xshd
 │   └─ wow_api.json
 ├─ Themes/
 │   ├─ Base.xaml
 │   ├─ Light.xaml
 │   └─ Dark.xaml
 ├─ MainWindow.xaml / .cs
 └─ README.md
```

---

## Contributing

PRs welcome! Good first issues:
- Better Lua grammar (keywords/strings/numbers/operators).
- Smarter symbol index (locals, method calls, require/module patterns).
- Live reload bridge addon (watch flag → `ReloadUI()`).
- Unit tests for .toc parser & validator.
- More theme polish (high-contrast, custom palettes).

---

## License

TBD – choose what fits your needs (MIT recommended for tools).

---

## Acknowledgements

- [AvalonEdit](https://github.com/icsharpcode/AvalonEdit)
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp)
- [Octokit.NET](https://github.com/octokit/octokit.net)
- [Ookii.Dialogs.Wpf](https://github.com/ookii-dialogs/ookii-dialogs-wpf)
- [DiffPlex](https://github.com/mmanela/diffplex)
