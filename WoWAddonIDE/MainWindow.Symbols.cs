using ICSharpCode.AvalonEdit;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private void RebuildSymbolIndex()
        {
            if (_project == null) return;
            _symbolIndex = SymbolService.BuildIndex(_project.RootPath);
            _symbolIndexBuilt = DateTime.Now;
            Log($"Symbol index: {_symbolIndex.Count} symbols.");
        }

        private void GoToDefinition(TextEditor ed)
        {
            if (_project == null) return;
            if ((DateTime.Now - _symbolIndexBuilt).TotalSeconds > 10) RebuildSymbolIndex();

            var word = GetWordAtOffset(ed.Text, ed.CaretOffset);
            if (string.IsNullOrWhiteSpace(word)) return;

            if (_symbolIndex.TryGetValue(word, out var locs) && locs.Count > 0)
            {
                var l = locs[0];
                OpenFileInTab(l.File);
                if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor target)
                {
                    target.ScrollToLine(l.Line);
                    target.CaretOffset = Math.Min(target.Document.TextLength, target.Document.GetOffset(l.Line, 1));
                    target.Focus();
                }
            }
            else
            {
                Status($"Definition not found: {word}");
            }
        }

        private void GoToSymbol_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) return;

            if ((DateTime.Now - _symbolIndexBuilt).TotalSeconds > 10)
                RebuildSymbolIndex();

            var dlg = new Windows.SymbolSearchWindow
            {
                Owner = this,
                Index = _symbolIndex.ToDictionary(k => k.Key, v => v.Value)
            };

            dlg.NavigateTo += loc =>
            {
                OpenFileInTab(loc.File);
                if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed)
                {
                    ed.ScrollToLine(loc.Line);
                    ed.CaretOffset = Math.Min(ed.Document.TextLength, ed.Document.GetOffset(loc.Line, 1));
                    ed.Focus();
                }
            };

            dlg.ShowDialog();
        }
    }
}