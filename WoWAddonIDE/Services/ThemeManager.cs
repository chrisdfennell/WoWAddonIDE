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
            // ensure Base.xaml is merged (App.xaml already does this, but no harm)
            MergeOnce("Themes/Base.xaml");
        }

        public static ThemeMode GetOsTheme()
        {
            // very cheap heuristic: check system theme resource; fallback to Light
            try
            {
                var isDark = (bool)Application.Current.Resources["PhoneDarkThemeVisibility"] == true;
                return isDark ? ThemeMode.Dark : ThemeMode.Light;
            }
            catch { return ThemeMode.Light; }
        }

        public static void ApplyTheme(ThemeMode mode)
        {
            // resolve 'System'
            if (mode == ThemeMode.System)
                mode = GetOsTheme();

            // remove any existing palette (Light/Dark) and add the selected one
            var appRes = Application.Current.Resources;
            var palettes = appRes.MergedDictionaries
                .Where(md => md.Source != null &&
                             (md.Source.OriginalString.EndsWith("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                              md.Source.OriginalString.EndsWith("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            foreach (var md in palettes) appRes.MergedDictionaries.Remove(md);

            Merge("Themes/" + (mode == ThemeMode.Dark ? "Dark.xaml" : "Light.xaml"));

            // nudge some global defaults
            appRes["TextElement.Foreground"] = appRes["Brush.FG"] as Brush ?? Brushes.Black;
            appRes["Window.Background"] = appRes["Brush.Window"] as Brush ?? Brushes.White;

            Settings.ThemeMode = mode;
            Persist?.Invoke();
            ThemeChanged?.Invoke();
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
            // try pack then relative
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
                catch { /* try next */ }
            }
        }

        public static void ApplyToEditor(TextEditor ed)
        {
            // Pull brushes from the active ResourceDictionary (with safe fallbacks)
            var bg = Try<Brush>("Brush.EditorBG") ?? Brushes.White;
            var fg = Try<Brush>("Brush.EditorFG") ?? Brushes.Black;
            var line = Try<Brush>("Brush.EditorLine") ?? new SolidColorBrush(Color.FromRgb(140, 140, 140));
            var sel = Try<Brush>("Brush.EditorSelection") ?? new SolidColorBrush(Color.FromArgb(64, 86, 156, 214));
            var borderPen =
                Try<Pen>("Brush.SelectionBorder") ??
                new Pen(line, 1);

            // Base editor colors
            ed.Background = bg;
            ed.Foreground = fg;

            // Selection visuals
            ed.TextArea.SelectionBrush = sel;
            ed.TextArea.SelectionCornerRadius = 2;
            ed.TextArea.SelectionBorder = borderPen;

            // Current line adornment
            ed.Options.HighlightCurrentLine = true;
            ed.TextArea.TextView.CurrentLineBorder = borderPen;

            // (Optional) faint background for current line if you add Brush.EditorCurrentLineBG
            var clBg = Try<Brush>("Brush.EditorCurrentLineBG");
            if (clBg != null)
                ed.TextArea.TextView.CurrentLineBackground = clBg;

            // Line numbers color (works across AvalonEdit versions)
            var lineNumberMargin = ed.TextArea.LeftMargins.FirstOrDefault(m => m.GetType().Name == "LineNumberMargin");
            if (lineNumberMargin != null)
            {
                // 1) Newer AvalonEdit exposes a TextBlock property
                var tbProp = lineNumberMargin.GetType().GetProperty("TextBlock", BindingFlags.Public | BindingFlags.Instance);
                if (tbProp?.GetValue(lineNumberMargin) is System.Windows.Controls.TextBlock tb)
                {
                    tb.Foreground = fg;
                }
                else
                {
                    // 2) Older versions keep a private brush field; try to set it reflectively
                    var field = lineNumberMargin.GetType().GetField("foreground", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null) field.SetValue(lineNumberMargin, fg);
                }
            }

            // Ensure visuals refresh
            ed.TextArea.TextView.InvalidateLayer(ICSharpCode.AvalonEdit.Rendering.KnownLayer.Background);
        }

        // Helper used above
        private static T? Try<T>(string key) where T : class =>
            System.Windows.Application.Current.TryFindResource(key) as T;
    }
}