using System;
using System.Linq;
using System.Windows;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class FindInFilesWindow : Window
    {
        public string ProjectRoot { get; set; } = "";
        public event Action<string, int, int>? NavigateTo;

        public FindInFilesWindow()
        {
            InitializeComponent();
            Extensions.Text = ".lua, .xml, .toc";
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            Results.ItemsSource = null;
            var q = Query.Text;
            if (string.IsNullOrWhiteSpace(q) || string.IsNullOrWhiteSpace(ProjectRoot))
                return;

            var flt = Extensions.Text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var hits = await FindInFiles.SearchAsync(ProjectRoot, q, Regex.IsChecked == true, Case.IsChecked == true, flt);
            Results.ItemsSource = hits.OrderBy(h => h.File).ThenBy(h => h.Line).ToList();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Results.MouseDoubleClick += (s, ev) =>
            {
                if (Results.SelectedItem is FindInFilesHit hit)
                {
                    NavigateTo?.Invoke(hit.File, hit.Line, hit.Col);
                    Close();
                }
            };
        }
    }
}