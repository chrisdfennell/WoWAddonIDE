using System.Windows;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Windows
{
    public partial class GitAuthWindow : Window
    {
        public IDESettings Settings { get; private set; }

        public GitAuthWindow(IDESettings settings)
        {
            InitializeComponent();
            Settings = settings;

            // populate
            UserName.Text = settings.GitUserName;
            UserEmail.Text = settings.GitUserEmail;
            ClientId.Text = settings.GitHubOAuthClientId;
            RemoteUrl.Text = settings.GitRemoteUrl;
            Token.Password = settings.GitHubToken;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            Settings.GitUserName = UserName.Text.Trim();
            Settings.GitUserEmail = UserEmail.Text.Trim();
            Settings.GitHubOAuthClientId = ClientId.Text.Trim();
            Settings.GitRemoteUrl = RemoteUrl.Text.Trim();
            Settings.GitHubToken = Token.Password;
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