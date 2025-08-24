// Services/ThemeManager.cs
using System;
using System.Windows;
using ICSharpCode.AvalonEdit;
using WoWAddonIDE.Models;
using Media = System.Windows.Media;

namespace WoWAddonIDE.Services
{
    public static class ThemeManager
    {
        // Single live instance other code reads
        public static IDESettings Settings { get; } = new IDESettings();

        // Persist hook (optional)
        public static Action? Persist;

        // Raised when ApplyTheme changes visuals
        public static event Action? ThemeChanged;

        // Call once at startup to copy loaded settings into the live instance
        public static void Initialize(IDESettings loaded)
        {
            UpdateFrom(loaded);
            ApplyTheme(Settings.ThemeMode);
        }

        // Copy fields into the live instance (no reference swap)
        public static void UpdateFrom(IDESettings src)
        {
            if (src == null) return;

            Settings.ThemeMode = src.ThemeMode;
            Settings.MainWindowWidth = src.MainWindowWidth;
            Settings.MainWindowHeight = src.MainWindowHeight;
            Settings.MainWindowMaximized = src.MainWindowMaximized;

            Settings.AddOnsPath = src.AddOnsPath;
            Settings.StagingPath = src.StagingPath;
            Settings.LastProjectPath = src.LastProjectPath;
            Settings.DefaultProjectRoot = src.DefaultProjectRoot;

            Settings.PackageExcludes = src.PackageExcludes;
            Settings.AllowBuildInsideAddOns = src.AllowBuildInsideAddOns;
            Settings.OpenAddOnsAfterBuild = src.OpenAddOnsAfterBuild;

            Settings.GitUserName = src.GitUserName;
            Settings.GitUserEmail = src.GitUserEmail;
            Settings.GitRemoteUrl = src.GitRemoteUrl;
            Settings.GitHubToken = src.GitHubToken;
            Settings.GitHubOAuthClientId = src.GitHubOAuthClientId;

            Settings.TabSize = src.TabSize;
            Settings.ConvertTabsToSpaces = src.ConvertTabsToSpaces;
            Settings.HighlightCurrentLine = src.HighlightCurrentLine;

            Settings.EditorFontFamily = src.EditorFontFamily;
            Settings.EditorFontSize = src.EditorFontSize;
            Settings.EditorWordWrap = src.EditorWordWrap;
            Settings.EditorShowInvisibles = src.EditorShowInvisibles;

            Settings.ApiDocsPath = src.ApiDocsPath;
        }

        // If some code still queries “OS theme”
        public static ThemeMode GetOsTheme() => ThemeMode.Light; // light-only for now

        public static void ApplyTheme(ThemeMode mode)
        {
            // Light-only: normalize any input to Light
            Settings.ThemeMode = ThemeMode.Light;

            // App-level brushes used by various windows
            var res = Application.Current?.Resources;
            if (res != null)
            {
                res["App.BackgroundBrush"] = System.Windows.Media.Brushes.White;
                res["App.ForegroundBrush"] = System.Windows.Media.Brushes.Black;
                res["Brush.Border"] = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(0x33, 0x00, 0x00, 0x00));
            }

            ThemeChanged?.Invoke();
            Persist?.Invoke();
        }

        public static void ApplyToEditor(TextEditor ed)
        {
            if (ed == null) return;

            // ===================== Base look (light) =====================
            ed.Background = System.Windows.Media.Brushes.White;
            ed.Foreground = System.Windows.Media.Brushes.Black;

            // Line numbers
            ed.ShowLineNumbers = true;
            ed.LineNumbersForeground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x79, 0x8A, 0x9C)); // brighter than DimGray

            // Selection (brighter blue-ish)
            ed.TextArea.SelectionBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x66, 0x33, 0x99, 0xFF)); // ~40% alpha
            ed.TextArea.SelectionBorder = null;

            // Current line (soft warm glow)
            ed.TextArea.TextView.CurrentLineBorder = null;
            ed.TextArea.TextView.CurrentLineBackground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xEE, 0xAA));

            // Caret color — use reflection so this compiles on all AvalonEdit versions
            try
            {
                var caret = ed.TextArea?.Caret;
                if (caret != null)
                {
                    var caretBrushProp = caret.GetType().GetProperty("CaretBrush");
                    var yellow = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(0xFF, 0xEE, 0x88)); // bright, visible

                    if (caretBrushProp != null && caretBrushProp.CanWrite)
                    {
                        caretBrushProp.SetValue(caret, yellow, null);
                    }
                    else
                    {
                        // Some versions expose CaretBrush on TextEditor itself
                        var editorCaretBrushProp = ed.GetType().GetProperty("CaretBrush");
                        if (editorCaretBrushProp != null && editorCaretBrushProp.CanWrite)
                            editorCaretBrushProp.SetValue(ed, yellow, null);
                    }
                }
            }
            catch
            {
                // Ignore — not available in all AvalonEdit builds
            }

            // ===================== Settings-driven options =====================
            var s = Settings;

            if (!string.IsNullOrWhiteSpace(s.EditorFontFamily))
                ed.FontFamily = new System.Windows.Media.FontFamily(s.EditorFontFamily);
            if (s.EditorFontSize > 0)
                ed.FontSize = s.EditorFontSize;

            // Indentation & tabs
            ed.Options.IndentationSize = Math.Max(1, s.EditorTabSize);
            ed.Options.ConvertTabsToSpaces = s.UseSpacesInsteadOfTabs;

            // UX toggles
            ed.Options.HighlightCurrentLine = s.EditorHighlightCurrentLine;
            ed.WordWrap = s.EditorWordWrap;

            // Invisibles
            ed.Options.ShowSpaces = s.EditorShowInvisibles;
            ed.Options.ShowTabs = s.EditorShowInvisibles;
            ed.Options.ShowEndOfLine = s.EditorShowInvisibles;
        }
    }
}
