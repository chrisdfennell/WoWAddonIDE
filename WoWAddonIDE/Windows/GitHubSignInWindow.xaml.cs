// File: WoWAddonIDE/Windows/GitHubSignInWindow.xaml.cs
using System;
using System.Threading;
using System.Windows;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    /// <summary>
    /// GitHub OAuth (Authorization Code + PKCE) sign-in window.
    /// - Uses Settings for ClientId/ClientSecret/Redirect.
    /// - Works with or without a client secret (pure PKCE supported).
    /// - Returns AccessToken when DialogResult == true.
    /// </summary>
    public partial class GitHubSignInWindow : Window
    {
        private readonly CancellationTokenSource _cts = new();

        private readonly string _clientId;
        private readonly string? _clientSecret; // optional
        private readonly string _redirect;      // must exactly match the URL registered in the GitHub OAuth app
        private readonly string _scope = "repo read:user";

        public string? AccessToken { get; private set; }

        // Convenience ctor: pull everything from Settings
        public GitHubSignInWindow()
            : this(
                Properties.Settings.Default.GitHubOAuthClientId ?? string.Empty,
                string.IsNullOrWhiteSpace(Properties.Settings.Default.GitHubOAuthClientSecret)
                    ? null
                    : Properties.Settings.Default.GitHubOAuthClientSecret,
                Properties.Settings.Default.GitHubOAuthRedirect ?? "http://localhost:53117/callback/")
        {
        }

        // Overload used by older call-sites that pass id/secret only
        public GitHubSignInWindow(string clientId, string? clientSecret)
            : this(
                clientId,
                clientSecret,
                Properties.Settings.Default.GitHubOAuthRedirect ?? "http://localhost:53117/callback/")
        {
        }

        // Main ctor
        public GitHubSignInWindow(string clientId, string? clientSecret, string redirect)
        {
            InitializeComponent();

            _clientId = (clientId ?? "").Trim();
            _clientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret.Trim();
            _redirect = NormalizeRedirect(redirect);
        }

        // XAML should wire a "Sign in" button to this handler (e.g., Click="OpenGitHub_Click")
        private async void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_clientId))
                {
                    MessageBox.Show(this,
                        "GitHub OAuth Client ID is empty. Set it in Tools → Settings.",
                        "GitHub Sign-in", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_redirect))
                {
                    MessageBox.Show(this,
                        "Redirect URL is empty. Set it in Tools → Settings.",
                        "GitHub Sign-in", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Run the full PKCE flow (opens browser, listens locally, exchanges code → token)
                var token = await GitHubAuthService.SignInWithPkceAsync(
                    clientId: _clientId,
                    clientSecret: _clientSecret,     // may be null for pure PKCE
                    redirect: _redirect,             // must match your registered callback exactly
                    scope: _scope,
                    ct: _cts.Token);

                if (!string.IsNullOrWhiteSpace(token))
                {
                    AccessToken = token;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(this,
                        "Sign-in was cancelled or failed.",
                        "GitHub Sign-in", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "GitHub Sign-in",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try { _cts.Cancel(); } catch { }
            DialogResult = false;
            Close();
        }

        private static string NormalizeRedirect(string redirect)
        {
            var r = (redirect ?? string.Empty).Trim();
            if (!r.EndsWith("/")) r += "/";
            return r;
        }
    }
}