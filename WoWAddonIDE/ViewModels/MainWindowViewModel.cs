using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.ViewModels
{
    /// <summary>
    /// ViewModel for MainWindow. Holds core application state and exposes
    /// bindable properties. This is the first step of a gradual MVVM migration —
    /// event handlers in MainWindow partials can progressively move logic here.
    /// </summary>
    public class MainWindowViewModel : ViewModelBase
    {
        // -------- Project State --------
        private AddonProject? _project;
        public AddonProject? Project
        {
            get => _project;
            set
            {
                if (SetProperty(ref _project, value))
                {
                    OnPropertyChanged(nameof(HasProject));
                    OnPropertyChanged(nameof(WindowTitle));
                    OnPropertyChanged(nameof(ProjectPathDisplay));
                }
            }
        }

        public bool HasProject => _project != null;

        public string WindowTitle => _project != null
            ? $"{_project.Name} — {Constants.AppName}"
            : Constants.AppName;

        public string ProjectPathDisplay => _project != null
            ? $"Project: {_project.RootPath}"
            : "AddOns Path: (not set) — Tools > Settings...";

        // -------- Status Bar --------
        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _caretPositionText = "";
        public string CaretPositionText
        {
            get => _caretPositionText;
            set => SetProperty(ref _caretPositionText, value);
        }

        private string _selectionText = "Sel 0";
        public string SelectionText
        {
            get => _selectionText;
            set => SetProperty(ref _selectionText, value);
        }

        private string _languageText = "Text";
        public string LanguageText
        {
            get => _languageText;
            set => SetProperty(ref _languageText, value);
        }

        private string _encodingText = "UTF-8";
        public string EncodingText
        {
            get => _encodingText;
            set => SetProperty(ref _encodingText, value);
        }

        private string _eolText = "EOL LF";
        public string EolText
        {
            get => _eolText;
            set => SetProperty(ref _eolText, value);
        }

        private string _gitBranchText = "";
        public string GitBranchText
        {
            get => _gitBranchText;
            set => SetProperty(ref _gitBranchText, value);
        }

        // -------- Settings --------
        private IDESettings _settings = new();
        public IDESettings Settings
        {
            get => _settings;
            set => SetProperty(ref _settings, value);
        }

        // -------- Symbol Index --------
        private Dictionary<string, List<SymbolService.SymbolLocation>> _symbolIndex = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<SymbolService.SymbolLocation>> SymbolIndex
        {
            get => _symbolIndex;
            set => SetProperty(ref _symbolIndex, value);
        }

        private DateTime _symbolIndexBuilt = DateTime.MinValue;
        public DateTime SymbolIndexBuilt
        {
            get => _symbolIndexBuilt;
            set => SetProperty(ref _symbolIndexBuilt, value);
        }

        // -------- Outline Items --------
        private IReadOnlyList<OutlineService.OutlineItem>? _outlineItems;
        public IReadOnlyList<OutlineService.OutlineItem>? OutlineItems
        {
            get => _outlineItems;
            set => SetProperty(ref _outlineItems, value);
        }

        // -------- Closed Tab History --------
        public Stack<string> ClosedTabPaths { get; } = new();

        // -------- Business Logic --------

        public void RebuildSymbolIndex()
        {
            if (Project == null) return;
            SymbolIndex = SymbolService.BuildIndex(Project.RootPath);
            SymbolIndexBuilt = DateTime.Now;
            LogService.Info($"Symbol index: {SymbolIndex.Count} symbols.");
        }

        public void EnsureSymbolIndexFresh(TimeSpan maxAge)
        {
            if ((DateTime.Now - SymbolIndexBuilt) > maxAge)
                RebuildSymbolIndex();
        }

        public void RefreshOutline(string? text, string? filePath)
        {
            if (text != null && filePath != null &&
                filePath.EndsWith(Constants.LuaExtension, StringComparison.OrdinalIgnoreCase))
            {
                OutlineItems = OutlineService.Build(text);
            }
            else
            {
                OutlineItems = null;
            }
        }

        public void UpdateGitStatus()
        {
            if (Project == null) { GitBranchText = ""; return; }
            try
            {
                var info = GitService.GetRepoInfo(Project.RootPath);
                GitBranchText =
                    $"{info.Branch ?? "(no branch)"}  ·  +{info.Ahead}/-{info.Behind}  " +
                    $"Δ {info.Added} / −{info.Deleted}  " +
                    (info.Conflicts > 0 ? $"⚠ {info.Conflicts} conflicts" : "");
            }
            catch
            {
                GitBranchText = "";
            }
        }

        public List<string> LintProject()
        {
            if (Project == null) return new List<string>();
            return LuaLint.Pass(Project);
        }
    }
}
