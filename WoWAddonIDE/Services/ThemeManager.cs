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

        /// <summary>Returns true if the effective theme is Dark.</summary>
        public static bool IsDark()
        {
            var mode = Settings.ThemeMode;
            if (mode == ThemeMode.System) mode = GetOsTheme();
            return mode == ThemeMode.Dark;
        }

        /// <summary>Reads the Windows "Apps use dark theme" registry value.</summary>
        public static ThemeMode GetOsTheme()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int i) return i == 0 ? ThemeMode.Dark : ThemeMode.Light;
            }
            catch (Exception ex) { LogService.Warn("Failed to read OS theme from registry", ex); }
            return ThemeMode.Light;
        }

        public static void ApplyTheme(ThemeMode mode)
        {
            Settings.ThemeMode = mode;
            bool dark = IsDark();

            var res = Application.Current?.Resources;
            if (res != null)
            {
                if (dark)
                {
                    // VS Code Dark+ inspired — neutral dark gray, not bluish
                    var bg      = new Media.SolidColorBrush(Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
                    var fg      = new Media.SolidColorBrush(Media.Color.FromRgb(0xD4, 0xD4, 0xD4));
                    var surface = new Media.SolidColorBrush(Media.Color.FromRgb(0x25, 0x25, 0x26));
                    var pressed = new Media.SolidColorBrush(Media.Color.FromRgb(0x37, 0x37, 0x3D));
                    var border  = new Media.SolidColorBrush(Media.Color.FromRgb(0x47, 0x47, 0x47));

                    res["App.BackgroundBrush"]       = bg;
                    res["App.ForegroundBrush"]        = fg;
                    res["App.BackgroundHoverBrush"]   = surface;
                    res["App.BackgroundPressedBrush"] = pressed;
                    res["App.BorderBrush"]            = border;
                    res["Brush.Border"]               = border;

                    var bgColor      = Media.Color.FromRgb(0x1E, 0x1E, 0x1E);
                    var fgColor      = Media.Color.FromRgb(0xD4, 0xD4, 0xD4);
                    var surfaceColor = Media.Color.FromRgb(0x25, 0x25, 0x26);
                    var borderColor  = Media.Color.FromRgb(0x47, 0x47, 0x47);
                    var highlight    = new Media.SolidColorBrush(Media.Color.FromRgb(0x26, 0x4F, 0x78));
                    var dimText      = new Media.SolidColorBrush(Media.Color.FromRgb(0x6E, 0x6E, 0x6E));

                    // Brush-based overrides
                    res[System.Windows.SystemColors.WindowBrushKey]                    = bg;
                    res[System.Windows.SystemColors.WindowTextBrushKey]                = fg;
                    res[System.Windows.SystemColors.ControlBrushKey]                   = surface;
                    res[System.Windows.SystemColors.ControlTextBrushKey]               = fg;
                    res[System.Windows.SystemColors.ControlLightBrushKey]              = surface;
                    res[System.Windows.SystemColors.ControlDarkBrushKey]               = border;
                    res[System.Windows.SystemColors.ControlLightLightBrushKey]         = surface;
                    res[System.Windows.SystemColors.ControlDarkDarkBrushKey]           = bg;
                    res[System.Windows.SystemColors.MenuBrushKey]                      = surface;
                    res[System.Windows.SystemColors.MenuBarBrushKey]                   = bg;
                    res[System.Windows.SystemColors.MenuTextBrushKey]                  = fg;
                    res[System.Windows.SystemColors.MenuHighlightBrushKey]             = pressed;
                    res[System.Windows.SystemColors.HighlightBrushKey]                 = highlight;
                    res[System.Windows.SystemColors.HighlightTextBrushKey]             = fg;
                    res[System.Windows.SystemColors.InactiveSelectionHighlightBrushKey] = surface;
                    res[System.Windows.SystemColors.ActiveBorderBrushKey]              = border;
                    res[System.Windows.SystemColors.InactiveBorderBrushKey]            = border;
                    res[System.Windows.SystemColors.GrayTextBrushKey]                  = dimText;

                    // Color-based overrides (some templates reference Color, not Brush)
                    res[System.Windows.SystemColors.WindowColorKey]       = bgColor;
                    res[System.Windows.SystemColors.WindowTextColorKey]   = fgColor;
                    res[System.Windows.SystemColors.ControlColorKey]      = surfaceColor;
                    res[System.Windows.SystemColors.ControlTextColorKey]  = fgColor;
                    res[System.Windows.SystemColors.MenuColorKey]         = surfaceColor;
                    res[System.Windows.SystemColors.MenuBarColorKey]      = bgColor;
                    res[System.Windows.SystemColors.MenuTextColorKey]     = fgColor;
                    res[System.Windows.SystemColors.HighlightColorKey]    = Media.Color.FromRgb(0x26, 0x4F, 0x78);
                    res[System.Windows.SystemColors.HighlightTextColorKey] = fgColor;
                }
                else
                {
                    // VS Code Light+ inspired — soft off-white, not harsh pure white
                    var bg      = new Media.SolidColorBrush(Media.Color.FromRgb(0xF3, 0xF3, 0xF3));
                    var fg      = new Media.SolidColorBrush(Media.Color.FromRgb(0x38, 0x3A, 0x42));
                    var surface = new Media.SolidColorBrush(Media.Color.FromRgb(0xE5, 0xE5, 0xE6));
                    var pressed = new Media.SolidColorBrush(Media.Color.FromRgb(0xD0, 0xD0, 0xD2));
                    var border  = new Media.SolidColorBrush(Media.Color.FromRgb(0xD4, 0xD4, 0xD4));

                    res["App.BackgroundBrush"]       = bg;
                    res["App.ForegroundBrush"]        = fg;
                    res["App.BackgroundHoverBrush"]   = surface;
                    res["App.BackgroundPressedBrush"] = pressed;
                    res["App.BorderBrush"]            = border;
                    res["Brush.Border"]               = border;

                    // Restore system defaults for light
                    foreach (var key in new object[]
                    {
                        System.Windows.SystemColors.WindowBrushKey,
                        System.Windows.SystemColors.WindowTextBrushKey,
                        System.Windows.SystemColors.ControlBrushKey,
                        System.Windows.SystemColors.ControlTextBrushKey,
                        System.Windows.SystemColors.ControlLightBrushKey,
                        System.Windows.SystemColors.ControlDarkBrushKey,
                        System.Windows.SystemColors.ControlLightLightBrushKey,
                        System.Windows.SystemColors.ControlDarkDarkBrushKey,
                        System.Windows.SystemColors.MenuBrushKey,
                        System.Windows.SystemColors.MenuBarBrushKey,
                        System.Windows.SystemColors.MenuTextBrushKey,
                        System.Windows.SystemColors.MenuHighlightBrushKey,
                        System.Windows.SystemColors.HighlightBrushKey,
                        System.Windows.SystemColors.HighlightTextBrushKey,
                        System.Windows.SystemColors.InactiveSelectionHighlightBrushKey,
                        System.Windows.SystemColors.ActiveBorderBrushKey,
                        System.Windows.SystemColors.InactiveBorderBrushKey,
                        System.Windows.SystemColors.GrayTextBrushKey,
                        System.Windows.SystemColors.WindowColorKey,
                        System.Windows.SystemColors.WindowTextColorKey,
                        System.Windows.SystemColors.ControlColorKey,
                        System.Windows.SystemColors.ControlTextColorKey,
                        System.Windows.SystemColors.MenuColorKey,
                        System.Windows.SystemColors.MenuBarColorKey,
                        System.Windows.SystemColors.MenuTextColorKey,
                        System.Windows.SystemColors.HighlightColorKey,
                        System.Windows.SystemColors.HighlightTextColorKey,
                    })
                    { res.Remove(key); }
                }
            }

            ThemeChanged?.Invoke();
            Persist?.Invoke();
        }

        public static void ApplyToEditor(TextEditor ed)
        {
            if (ed == null) return;

            bool dark = IsDark();

            if (dark)
            {
                // VS Code Dark+ editor colors
                ed.Background = new Media.SolidColorBrush(Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
                ed.Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0xD4, 0xD4, 0xD4));
                ed.ShowLineNumbers = true;
                ed.LineNumbersForeground = new Media.SolidColorBrush(Media.Color.FromRgb(0x85, 0x85, 0x85));
                ed.TextArea.SelectionBrush = new Media.SolidColorBrush(Media.Color.FromArgb(0xCC, 0x26, 0x4F, 0x78));
                ed.TextArea.SelectionBorder = null;
                ed.TextArea.TextView.CurrentLineBorder = null;
                ed.TextArea.TextView.CurrentLineBackground =
                    new Media.SolidColorBrush(Media.Color.FromArgb(0x40, 0x2A, 0x2D, 0x2E));
            }
            else
            {
                // VS Code Light+ editor colors
                ed.Background = new Media.SolidColorBrush(Media.Color.FromRgb(0xFF, 0xFF, 0xFF));
                ed.Foreground = new Media.SolidColorBrush(Media.Color.FromRgb(0x38, 0x3A, 0x42));
                ed.ShowLineNumbers = true;
                ed.LineNumbersForeground = new Media.SolidColorBrush(Media.Color.FromRgb(0x23, 0x78, 0x93));
                ed.TextArea.SelectionBrush = new Media.SolidColorBrush(Media.Color.FromArgb(0xB0, 0xAD, 0xD6, 0xFF));
                ed.TextArea.SelectionBorder = null;
                ed.TextArea.TextView.CurrentLineBorder = null;
                ed.TextArea.TextView.CurrentLineBackground =
                    new Media.SolidColorBrush(Media.Color.FromArgb(0x50, 0xEB, 0xF1, 0xF8));
            }

            // Caret — white in dark mode, black in light mode (always visible)
            try
            {
                var caretColor = dark
                    ? new Media.SolidColorBrush(Media.Color.FromRgb(0xAE, 0xAF, 0xAD))
                    : new Media.SolidColorBrush(Media.Color.FromRgb(0x00, 0x00, 0x00));

                var caret = ed.TextArea?.Caret;
                if (caret != null)
                {
                    var caretBrushProp = caret.GetType().GetProperty("CaretBrush");
                    if (caretBrushProp != null && caretBrushProp.CanWrite)
                    {
                        caretBrushProp.SetValue(caret, caretColor, null);
                    }
                    else
                    {
                        var edProp = ed.GetType().GetProperty("CaretBrush");
                        if (edProp != null && edProp.CanWrite)
                            edProp.SetValue(ed, caretColor, null);
                    }
                }
            }
            catch (Exception ex) { LogService.Warn("Failed to set editor caret brush", ex); }

            // ===================== Settings-driven options =====================
            var s = Settings;

            if (!string.IsNullOrWhiteSpace(s.EditorFontFamily))
                ed.FontFamily = new Media.FontFamily(s.EditorFontFamily);
            if (s.EditorFontSize > 0)
                ed.FontSize = s.EditorFontSize;

            ed.Options.IndentationSize = Math.Max(1, s.EditorTabSize);
            ed.Options.ConvertTabsToSpaces = s.UseSpacesInsteadOfTabs;
            ed.Options.HighlightCurrentLine = s.EditorHighlightCurrentLine;
            ed.WordWrap = s.EditorWordWrap;

            ed.Options.ShowSpaces = s.EditorShowInvisibles;
            ed.Options.ShowTabs = s.EditorShowInvisibles;
            ed.Options.ShowEndOfLine = s.EditorShowInvisibles;
        }
    }
}
