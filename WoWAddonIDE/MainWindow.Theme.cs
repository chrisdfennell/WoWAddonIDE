using ICSharpCode.AvalonEdit;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private bool IsDarkThemeActive()
        {
            var mode = _settings.ThemeMode == ThemeMode.System
                ? ThemeManager.GetOsTheme()
                : _settings.ThemeMode;
            return mode == ThemeMode.Dark;
        }

        private void Toolbar_ThemeCycle_Click(object sender, RoutedEventArgs e)
        {
            var next = _settings.ThemeMode switch
            {
                ThemeMode.System => ThemeMode.Light,
                ThemeMode.Light => ThemeMode.Dark,
                _ => ThemeMode.System
            };
            ThemeManager.ApplyTheme(next);
            _settings.ThemeMode = next;
            SaveSettings();

            foreach (var ed in AllEditors())
            {
                ThemeManager.ApplyToEditor(ed);
                if (ed.SyntaxHighlighting != null)
                    RetintHighlighting(ed.SyntaxHighlighting, IsDarkThemeActive());
            }
            Status($"Theme: {next}");
        }

        private void ApplyThemeToEditor(TextEditor ed)
        {
            ThemeManager.ApplyToEditor(ed);
            if (ed.SyntaxHighlighting != null)
                RetintHighlighting(ed.SyntaxHighlighting, IsDarkThemeActive());
        }

        private void ThemeSystem_Click(object s, RoutedEventArgs e) { ThemeManager.ApplyTheme(ThemeMode.System); SaveSettings(); }
        private void ThemeLight_Click(object s, RoutedEventArgs e) { ThemeManager.ApplyTheme(ThemeMode.Light); SaveSettings(); }
        private void ThemeDark_Click(object s, RoutedEventArgs e) { ThemeManager.ApplyTheme(ThemeMode.Dark); SaveSettings(); }

        internal void ReapplyEditorThemeToOpenTabs()
        {
            foreach (var tab in EditorTabs.Items.Cast<TabItem>())
            {
                var editor = FindDescendant<TextEditor>(tab);
                if (editor != null)
                {
                    // Fully qualify System.Windows.Media.FontFamily to resolve ambiguity
                    editor.FontFamily = new System.Windows.Media.FontFamily(ThemeManager.Settings.EditorFontFamily);
                    editor.FontSize = ThemeManager.Settings.EditorFontSize;
                    editor.Options.IndentationSize = ThemeManager.Settings.EditorTabSize;
                    editor.WordWrap = ThemeManager.Settings.EditorWordWrap;
                    ThemeManager.ApplyToEditor(editor);
                }
            }
        }

        private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) return t;
                var deeper = FindDescendant<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}
