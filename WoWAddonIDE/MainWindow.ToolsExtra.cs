using System.Windows;

namespace WoWAddonIDE
{
    public partial class MainWindow
    {
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