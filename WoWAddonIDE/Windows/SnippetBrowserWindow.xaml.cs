using System.Windows;
using System.Windows.Controls;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class SnippetBrowserWindow : Window
    {
        /// <summary>The snippet body the user chose to insert, or null if cancelled.</summary>
        public string? SelectedSnippetBody { get; private set; }

        public SnippetBrowserWindow()
        {
            InitializeComponent();

            foreach (var (trigger, title, _) in Snippets.All)
                SnippetList.Items.Add($"{trigger}  —  {title}");

            if (SnippetList.Items.Count > 0)
                SnippetList.SelectedIndex = 0;
        }

        private void SnippetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var idx = SnippetList.SelectedIndex;
            if (idx < 0 || idx >= Snippets.All.Length) return;

            var (trigger, title, body) = Snippets.All[idx];
            PreviewBox.Text = body;
            TriggerLabel.Text = $"Trigger: {trigger}  |  Type \"{trigger}\" in the editor for autocomplete";
        }

        private void SnippetList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            InsertSelected();
        }

        private void Insert_Click(object sender, RoutedEventArgs e)
        {
            InsertSelected();
        }

        private void InsertSelected()
        {
            var idx = SnippetList.SelectedIndex;
            if (idx < 0 || idx >= Snippets.All.Length) return;

            SelectedSnippetBody = Snippets.All[idx].Body;
            DialogResult = true;
            Close();
        }
    }
}
