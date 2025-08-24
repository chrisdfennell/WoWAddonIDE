using Microsoft.Win32;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Services
{
    public static class ThemeManager
    {
        public static IDESettings Settings { get; private set; } = new IDESettings();
        public static Action? Persist { get; set; }
        public static event Action? ThemeChanged;

        public static void Initialize(IDESettings settings)
        {
            Settings = settings ?? new IDESettings();
            // Ensure base dictionary is present (defines Brush.* keys, etc.)
            MergeOnce("Themes/Base.xaml");

            // Optional: auto-refresh when Windows theme changes (only while using System mode)
            SystemEvents.UserPreferenceChanged += (_, __) =>
            {
                if (Settings.ThemeMode == ThemeMode.System)
                    ApplyTheme(ThemeMode.System);
            };
        }

        /// <summary>
        /// Windows 10/11: HKCU\...\Personalize\AppsUseLightTheme (0=Dark, 1=Light).
        /// Falls back to Light if missing.
        /// </summary>
        public static ThemeMode GetOsTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var value = key?.GetValue("AppsUseLightTheme");
                if (value is int i)
                    return i == 0 ? ThemeMode.Dark : ThemeMode.Light;
            }
            catch { /* ignore */ }

            return ThemeMode.Light;
        }

        /// <summary>
        /// Apply the requested theme. If System is requested, resolves to OS theme, 
        /// but still saves the user's requested mode in Settings.
        /// </summary>
        public static void ApplyTheme(ThemeMode requested)
        {
            var effective = requested == ThemeMode.System ? GetOsTheme() : requested;

            var appRes = Application.Current.Resources;

            // Remove any existing Light/Dark palette
            var palettes = appRes.MergedDictionaries
                .Where(md => md.Source != null &&
                             (md.Source.OriginalString.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                              md.Source.OriginalString.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase)))
                .ToList();
            foreach (var md in palettes)
                appRes.MergedDictionaries.Remove(md);

            // Merge the effective palette
            Merge("Themes/" + (effective == ThemeMode.Dark ? "Dark.xaml" : "Light.xaml"));

            // Provide App.* aliases so existing XAML stays happy
            Alias(appRes, "App.BackgroundBrush", "Brush.Window", Brushes.White);
            Alias(appRes, "App.ForegroundBrush", "Brush.FG", Brushes.Black);
            Alias(appRes, "App.AccentBrush", "Brush.Accent", Brushes.SteelBlue);

            // Some defaults used by controls that read these keys
            appRes["TextElement.Foreground"] = Try<Brush>("Brush.FG") ?? Brushes.Black;
            appRes["Window.Background"] = Try<Brush>("Brush.Window") ?? Brushes.White;

            // Persist the user's CHOSEN mode (not the resolved one)
            Settings.ThemeMode = requested;
            Persist?.Invoke();
            ThemeChanged?.Invoke();
        }

        private static void Alias(ResourceDictionary res, string aliasKey, string sourceKey, object fallback)
        {
            res[aliasKey] = Try<object>(sourceKey) ?? fallback;
        }

        private static void MergeOnce(string relative)
        {
            var appRes = Application.Current.Resources;
            if (appRes.MergedDictionaries.Any(md => md.Source != null &&
                md.Source.OriginalString.EndsWith(relative, StringComparison.OrdinalIgnoreCase)))
                return;

            Merge(relative);
        }

        private static void Merge(string relative)
        {
            var asmName = Assembly.GetExecutingAssembly().GetName().Name ?? "WoWAddonIDE";
            var candidates = new[]
            {
                new Uri($"/{asmName};component/{relative}", UriKind.Relative),
                new Uri(relative, UriKind.Relative)
            };
            foreach (var u in candidates)
            {
                try
                {
                    var rd = new ResourceDictionary { Source = u };
                    Application.Current.Resources.MergedDictionaries.Add(rd);
                    return;
                }
                catch
                {
                    // try next
                }
            }
        }

        /// <summary>
        /// Apply current theme brushes to an AvalonEdit editor.
        /// Handles both new/old AvalonEdit variants for line numbers.
        /// </summary>
        public static void ApplyToEditor(TextEditor ed)
        {
            // Brushes from theme with safe fallbacks
            var bg = Try<Brush>("Brush.EditorBG") ?? Brushes.White;
            var fg = Try<Brush>("Brush.EditorFG") ?? Brushes.Black;
            var line = Try<Brush>("Brush.EditorLine") ?? new SolidColorBrush(Color.FromRgb(140, 140, 140));
            var sel = Try<Brush>("Brush.EditorSelection") ?? new SolidColorBrush(Color.FromArgb(0x33, 0x5E, 0x81, 0xCE));

            // Selection/line-border Pen:
            // - If theme provided a Pen, use it.
            // - Else if it provided a Brush (common), wrap into a Pen.
            // - Else fall back to 'line' brush.
            var borderPen = Try<Pen>("Brush.SelectionBorder")
                            ?? MakePen(Try<Brush>("Brush.SelectionBorder"))
                            ?? new Pen(line, 1);

            // Base editor colors
            ed.Background = bg;
            ed.Foreground = fg;

            // Selection visuals
            ed.TextArea.SelectionBrush = sel;
            // Some versions expose CornerRadius; if yours doesn't, comment this out.
            try { ed.TextArea.SelectionCornerRadius = 2; } catch { /* older AvalonEdit */ }
            ed.TextArea.SelectionBorder = borderPen;

            // Current line adornment
            ed.Options.HighlightCurrentLine = true;
            ed.TextArea.TextView.CurrentLineBorder = borderPen;

            // Optional themed current-line background
            var clBg = Try<Brush>("Brush.EditorCurrentLineBG");
            if (clBg != null)
                ed.TextArea.TextView.CurrentLineBackground = clBg;

            // Line numbers foreground across versions
            var lnMargin = ed.TextArea.LeftMargins.FirstOrDefault(m => m.GetType().Name == "LineNumberMargin");
            if (lnMargin != null)
            {
                // 1) Newer AvalonEdit has a public TextBlock
                var tbProp = lnMargin.GetType().GetProperty("TextBlock", BindingFlags.Public | BindingFlags.Instance);
                if (tbProp?.GetValue(lnMargin) is System.Windows.Controls.TextBlock tb)
                {
                    tb.Foreground = fg;
                }
                else
                {
                    // 2) Older versions: try private field "foreground"
                    var field = lnMargin.GetType().GetField("foreground", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null) field.SetValue(lnMargin, fg);
                }
            }

            // Refresh visuals
            ed.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Background);
        }

        private static Pen MakePen(Brush? b)
            => new Pen(b ?? Brushes.Transparent, 1);

        // Resource helper
        private static T? Try<T>(string key) where T : class =>
            Application.Current?.TryFindResource(key) as T;
    }
}