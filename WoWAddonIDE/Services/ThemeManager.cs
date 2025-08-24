// Services/ThemeManager.cs
using System;
using System.Reflection;                 // for reflection on CaretBrush
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Services
{
    public static class ThemeManager
    {
        // Single live instance other code reads
        public static IDESettings Settings { get; } = new IDESettings();

        // Persist hook you wire in App.xaml.cs
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
        public static ThemeMode GetOsTheme() => ThemeMode.Light; // you chose light-only now

        public static void ApplyTheme(ThemeMode mode)
        {
            // Light-only: normalize any input to Light
            Settings.ThemeMode = ThemeMode.Light;

            // App-level brushes used by various windows
            var res = Application.Current?.Resources;
            if (res != null)
            {
                res["App.BackgroundBrush"] = Brushes.White;
                res["App.ForegroundBrush"] = Brushes.Black;
                res["Brush.Border"] = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x00, 0x00));
            }

            ThemeChanged?.Invoke();
        }

        public static void ApplyToEditor(TextEditor ed)
        {
            if (ed == null) return;

            // Light theme look
            ed.Background = Brushes.White;
            ed.Foreground = Brushes.Black;

            // Line numbers & selection
            ed.ShowLineNumbers = true;
            ed.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x88, 0x99, 0xAA));
            ed.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x33, 0x99, 0xFF));
            ed.TextArea.SelectionBorder = null;

            // Subtle current line highlight
            ed.TextArea.TextView.CurrentLineBorder = null;
            ed.TextArea.TextView.CurrentLineBackground =
                new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));

            // Caret color — use reflection so this compiles on all AvalonEdit versions
            try
            {
                // Prefer TextArea.Caret.CaretBrush if it exists
                var caret = ed.TextArea.Caret;
                var caretBrushProp = caret.GetType().GetProperty("CaretBrush", BindingFlags.Public | BindingFlags.Instance);
                if (caretBrushProp != null && caretBrushProp.CanWrite)
                {
                    caretBrushProp.SetValue(caret, Brushes.Black, null);
                }
                else
                {
                    // Some versions expose CaretBrush on TextEditor
                    var editorCaretBrushProp = ed.GetType().GetProperty("CaretBrush", BindingFlags.Public | BindingFlags.Instance);
                    if (editorCaretBrushProp != null && editorCaretBrushProp.CanWrite)
                        editorCaretBrushProp.SetValue(ed, Brushes.Black, null);
                }
            }
            catch
            {
                // If neither property exists, we just keep the default caret
            }

            // Fonts & editor options from settings
            var s = Settings;
            if (!string.IsNullOrWhiteSpace(s.EditorFontFamily))
                ed.FontFamily = new FontFamily(s.EditorFontFamily);
            if (s.EditorFontSize > 0)
                ed.FontSize = s.EditorFontSize;

            // Use the friendly alias properties if your IDESettings exposes them;
            // otherwise map to the legacy fields in your IDESettings implementation.
            ed.Options.IndentationSize = Math.Max(1, s.EditorTabSize);
            ed.Options.ConvertTabsToSpaces = s.UseSpacesInsteadOfTabs;
            ed.Options.HighlightCurrentLine = s.EditorHighlightCurrentLine;
            ed.WordWrap = s.EditorWordWrap;

            // Invisibles
            ed.Options.ShowSpaces = s.EditorShowInvisibles;
            ed.Options.ShowTabs = s.EditorShowInvisibles;
            ed.Options.ShowEndOfLine = s.EditorShowInvisibles;
        }
    }
}