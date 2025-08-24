using System.Linq;
using System.Windows;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class BlameWindow : Window
    {
        public BlameWindow() { InitializeComponent(); }

        public void LoadBlame(string repoRoot, string absoluteFile)
        {
            Header.Text = absoluteFile;
            var rows = GitService.Blame(repoRoot, absoluteFile)
                .Select(b => new
                {
                    Lines = $"{b.StartLine}-{b.StartLine + b.LineCount - 1}",
                    ShaShort = b.CommitSha.Substring(0, 8),
                    b.Author,
                    When = b.When.ToString("yyyy-MM-dd HH:mm"),
                    b.Summary
                });
            List.ItemsSource = rows.ToList();
        }
    }
}
