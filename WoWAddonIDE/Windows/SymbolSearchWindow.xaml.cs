using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using WoWAddonIDE.Services; // <-- important

namespace WoWAddonIDE.Windows
{
    public partial class SymbolSearchWindow : Window
    {
        // Use the nested type: SymbolService.SymbolLocation
        public Dictionary<string, List<SymbolService.SymbolLocation>> Index { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);

        public event Action<SymbolService.SymbolLocation>? NavigateTo;

        public SymbolSearchWindow()
        {
            InitializeComponent();
        }

        private void Query_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var q = Query.Text.Trim();
            if (string.IsNullOrEmpty(q)) { Results.ItemsSource = null; return; }

            var hits =
                from kv in Index
                where kv.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                from l in kv.Value
                orderby kv.Key.Length, kv.Key
                select l;

            Results.ItemsSource = hits.Take(200).ToList();
        }

        private void Results_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Results.SelectedItem is SymbolService.SymbolLocation loc)
            {
                NavigateTo?.Invoke(loc);
                Close();
            }
        }
    }
}