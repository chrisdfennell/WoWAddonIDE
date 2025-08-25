using ICSharpCode.AvalonEdit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private void RefreshOutlineForActive()
        {
            if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed && (tab.Tag as string) is string path)
            {
                if (path.EndsWith(".lua", System.StringComparison.OrdinalIgnoreCase))
                {
                    var items = OutlineService.Build(ed.Text);
                    Outline.ItemsSource = items;
                    return;
                }
            }
            Outline.ItemsSource = null;
        }

        private void Outline_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Outline.SelectedItem is OutlineService.OutlineItem it)
            {
                if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed)
                {
                    var off = GetOffsetForLine(ed.Text, it.Line);
                    ed.SelectionStart = off;
                    ed.SelectionLength = 0;
                    ed.ScrollToLine(it.Line);
                    ed.Focus();
                }
            }
        }

        private static int GetOffsetForLine(string text, int line)
        {
            int current = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (current == line) return i;
                if (text[i] == '\n') current++;
            }
            return text.Length;
        }
    }
}