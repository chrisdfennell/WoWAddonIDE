using System.Windows;
using ICSharpCode.AvalonEdit;

namespace WoWAddonIDE
{
    public partial class MainWindow
    {
        // Menu: Tools → WoW API Browser…
        private void OpenApiBrowser_Click(object sender, RoutedEventArgs e)
        {
            var win = new WoWAddonIDE.Windows.ApiDocsBrowserWindow(
                _completion.ApiEntries,
                insertCallback: text =>
                {
                    if (TryGetActiveEditor(out var ed, out _))
                        ed.Document.Insert(ed.CaretOffset, text);
                });
            win.Owner = this;
            win.Show();
        }

        // Menu: Tools → Frame XML Designer…
        private void OpenFrameDesigner_Click(object sender, RoutedEventArgs e)
        {
            var win = new WoWAddonIDE.Windows.FrameDesignerWindow(
                insertCallback: xml =>
                {
                    if (TryGetActiveEditor(out var ed, out _))
                        ed.Document.Insert(ed.CaretOffset, xml);
                });
            win.Owner = this;
            win.Show();
        }

        // Menu: Tools → Lua REPL (switch to REPL tab in bottom panel)
        private void OpenLuaRepl_Click(object sender, RoutedEventArgs e)
        {
            // Find and select the Lua REPL tab in the bottom TabControl
            var bottomTabControl = ReplOutput?.Parent;
            while (bottomTabControl != null && bottomTabControl is not System.Windows.Controls.TabControl)
            {
                bottomTabControl = (bottomTabControl as FrameworkElement)?.Parent;
            }

            if (bottomTabControl is System.Windows.Controls.TabControl tc)
            {
                foreach (System.Windows.Controls.TabItem tab in tc.Items)
                {
                    if (tab.Header?.ToString() == "Lua REPL")
                    {
                        tc.SelectedItem = tab;
                        ReplInput?.Focus();
                        break;
                    }
                }
            }
        }
        // Menu/Toolbar: Tools → Command Palette…
        public void OpenCommandPalette_Click(object? sender, RoutedEventArgs e)
        {
            // Works whether your window has an overload that takes MainWindow or not.
            WoWAddonIDE.Windows.CommandPaletteWindow dlg;

            var ctor = typeof(WoWAddonIDE.Windows.CommandPaletteWindow)
                .GetConstructor(new[] { typeof(MainWindow) });

            if (ctor != null)
                dlg = (WoWAddonIDE.Windows.CommandPaletteWindow)ctor.Invoke(new object[] { this });
            else
                dlg = new WoWAddonIDE.Windows.CommandPaletteWindow();

            dlg.Owner = this;

            // If your palette exposes Init(MainWindow) instead of a ctor, call it reflectively
            var init = dlg.GetType().GetMethod("Init", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            init?.Invoke(dlg, new object[] { this });

            dlg.ShowDialog();
        }

        // Menu: Tools → SavedVariables Inspector…
        private void OpenSavedVariables_Click(object sender, RoutedEventArgs e)
        {

            // If you already made a window, this is what you'd do:
            var win = new WoWAddonIDE.Windows.SavedVariablesWindow(_settings);
            win.Owner = this;
            win.ShowDialog();
        }
    }
}