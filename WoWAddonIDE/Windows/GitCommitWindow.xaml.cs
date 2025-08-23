using System.Windows;

namespace WoWAddonIDE.Windows
{
    public partial class GitCommitWindow : Window
    {
        public string CommitMessage { get; private set; } = "";

        public GitCommitWindow()
        {
            InitializeComponent();
            Loaded += (_, __) => CommitMessageBox.Focus();
        }

        private void Commit_Click(object sender, RoutedEventArgs e)
        {
            CommitMessage = CommitMessageBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}