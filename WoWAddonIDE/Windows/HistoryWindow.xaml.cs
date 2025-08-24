using System.Linq;
using System.Windows;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class HistoryWindow : Window
    {
        private string _repoRoot = "";
        private string _file = "";
        private System.Collections.Generic.List<dynamic> _rows = new();

        public HistoryWindow() { InitializeComponent(); }

        public void LoadHistory(string repoRoot, string absoluteFile)
        {
            _repoRoot = repoRoot; _file = absoluteFile;
            var list = GitService.FileHistory(repoRoot, absoluteFile).ToList();
            _rows = list.Select(c => new
            {
                Sha = c.Sha,
                ShaShort = c.Sha.Substring(0, 8),
                c.Author,
                When = c.When.ToString("yyyy-MM-dd HH:mm"),
                c.Message
            }).Cast<dynamic>().ToList();
            List.ItemsSource = _rows;
        }

        private void DiffPrev_Click(object sender, RoutedEventArgs e)
        {
            if (List.SelectedIndex < 0 || List.SelectedIndex >= _rows.Count) return;
            var cur = _rows[List.SelectedIndex];
            var prev = (List.SelectedIndex + 1 < _rows.Count) ? _rows[List.SelectedIndex + 1] : null;
            if (prev == null) { MessageBox.Show(this, "No previous commit."); return; }

            var a = GitService.GetFileContentAtCommit(_repoRoot, _file, (string)prev.Sha);
            var b = GitService.GetFileContentAtCommit(_repoRoot, _file, (string)cur.Sha);

            var dw = new DiffWindow { Owner = this };
            dw.ShowDiff(a ?? "", b ?? "");
            dw.ShowDialog();
        }
    }
}