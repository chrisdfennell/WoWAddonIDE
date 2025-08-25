// File: WoWAddonIDE/Windows/GitHubSignInWindow.xaml.cs
using System;
using System.Threading;
using System.Windows;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class GitHubSignInWindow : Window
    {
        private readonly string _clientId;
        private readonly string? _clientSecret;
        private readonly string _scope;
        private readonly int _port;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>The OAuth access token returned by GitHub when the dialog succeeds.</summary>
        public string? AccessToken { get; private set; }

        /// <summary>
        /// Sign-in dialog using Authorization Code + PKCE (no manual code entry).
        /// </summary>
        public GitHubSignInWindow(string clientId, string? clientSecret = null, string scope = "repo read:user", int port = 48123)
        {
            InitializeComponent();
            _clientId = clientId;
            _clientSecret = clientSecret;
            _scope = scope;
            _port = port;

            // Hide the old device-flow box if it's in the XAML
            try
            {
                if (FindName("UserCodeBox") is System.Windows.Controls.TextBox tb)
                    tb.Visibility = Visibility.Collapsed;
            }
            catch { /* ignore */ }

            Closed += (_, __) => _cts.Cancel();
        }

        private async void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement fe) fe.IsEnabled = false;

                var token = await GitHubAuthService.SignInViaPkceAsync(
                    _clientId, _clientSecret, _scope, _port, _cts.Token);

                AccessToken = token;
                DialogResult = true;
                Close();
            }
            catch (OperationCanceledException)
            {
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "GitHub Sign-in",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is FrameworkElement fe) fe.IsEnabled = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _cts.Cancel();
            DialogResult = false;
            Close();
        }
    }
}