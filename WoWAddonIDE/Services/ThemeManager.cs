using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Services
{
    public static class ThemeManager
    {
        // Compatibility shim for old callers – we no longer detect OS theme.
        public static ThemeMode GetOsTheme() => ThemeMode.Light;

        public static IDESettings Settings { get; private set; } = new IDESettings();
        public static Action? Persist { get; set; }
        public static event Action? ThemeChanged;

        public static void Initialize(IDESettings settings)
        {
            Settings = settings ?? new IDESettings();
            ApplyLightTheme();   // always light
        }

        public static void ApplyTheme(ThemeMode _) => ApplyLightTheme(); // keep signature, ignore arg

        private static void ApplyLightTheme()
        {
            var app = Application.Current.Resources;

            // Ensure the expected keys exist (in case App.xaml missed them)
            app["App.BackgroundBrush"] = app["App.BackgroundBrush"] ?? new SolidColorBrush(Colors.White);
            app["App.ForegroundBrush"] = app["App.ForegroundBrush"] ?? new SolidColorBrush(Color.FromRgb(30, 30, 30));
            app["Brush.Window"] = app["Brush.Window"] ?? app["App.BackgroundBrush"];
            app["Brush.Border"] = app["Brush.Border"] ?? new SolidColorBrush(Color.FromRgb(204, 204, 204));

            app["Brush.EditorBG"] = app["Brush.EditorBG"] ?? new SolidColorBrush(Colors.White);
            app["Brush.EditorFG"] = app["Brush.EditorFG"] ?? new SolidColorBrush(Colors.Black);
            app["Brush.EditorLine"] = app["Brush.EditorLine"] ?? new SolidColorBrush(Color.FromRgb(229, 229, 229));
            app["Brush.EditorCurrentLineBG"] = app["Brush.EditorCurrentLineBG"] ?? new SolidColorBrush(Color.FromRgb(247, 247, 247));
            app["Brush.EditorSelection"] = app["Brush.EditorSelection"] ?? new SolidColorBrush(Color.FromRgb(208, 231, 255));
            app["Brush.SelectionBorder"] = app["Brush.SelectionBorder"] ?? new Pen(new SolidColorBrush(Color.FromRgb(134, 165, 198)), 1);

            // Some global defaults (optional)
            app["TextElement.Foreground"] = app["App.ForegroundBrush"];
            app["Window.Background"] = app["Brush.Window"];

            Settings.ThemeMode = ThemeMode.Light;  // keep persisted value consistent
            Persist?.Invoke();
            ThemeChanged?.Invoke();
        }

        public static void ApplyToEditor(TextEditor ed)
        {
            Brush bg = appRes<Brush>("Brush.EditorBG") ?? Brushes.White;
            Brush fg = appRes<Brush>("Brush.EditorFG") ?? Brushes.Black;
            Brush line = appRes<Brush>("Brush.EditorLine") ?? new SolidColorBrush(Color.FromRgb(229, 229, 229));
            Brush sel = appRes<Brush>("Brush.EditorSelection") ?? new SolidColorBrush(Color.FromRgb(208, 231, 255));
            Pen selBorder = appRes<Pen>("Brush.SelectionBorder") ?? new Pen(line, 1);

            ed.Background = bg;
            ed.Foreground = fg;

            ed.TextArea.SelectionBrush = sel;
            ed.TextArea.SelectionBorder = selBorder;
            ed.TextArea.SelectionCornerRadius = 2;

            ed.Options.HighlightCurrentLine = true;
            ed.TextArea.TextView.CurrentLineBorder = new Pen(line, 1);
            ed.TextArea.TextView.CurrentLineBackground = appRes<Brush>("Brush.EditorCurrentLineBG");

            ed.TextArea.TextView.InvalidateVisual();
        }

        private static T? appRes<T>(string key) where T : class =>
            Application.Current.TryFindResource(key) as T;
    }
}