using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Windows
{
    public partial class GitHubSignInWindow : Window
    {
        private readonly string _clientId;
        private readonly string _scope;
        private GitHubDeviceCode? _dc;
        private readonly CancellationTokenSource _cts = new();

        public string? AccessToken { get; private set; }

        public GitHubSignInWindow(string clientId, string scope = "repo read:user")
        {
            InitializeComponent();
            _clientId = clientId;
            _scope = scope;
            Loaded += async (_, __) => await BeginAsync();
            Closed += (_, __) => _cts.Cancel();
        }

        private async Task BeginAsync()
        {
            try
            {
                _dc = await GitHubAuthService.BeginDeviceFlowAsync(_clientId, _scope, _cts.Token);
                UserCodeBox.Text = _dc.user_code;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "GitHub Sign-in", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void OpenGitHub_Click(object sender, RoutedEventArgs e)
        {
            if (_dc == null) return;
            Process.Start(new ProcessStartInfo { FileName = _dc.verification_uri, UseShellExecute = true });

            // Start polling
            _ = PollAsync();
        }

        private async Task PollAsync()
        {
            if (_dc == null) return;
            try
            {
                var token = await GitHubAuthService.WaitForAccessTokenAsync(
                    _clientId, _dc.device_code, _dc.interval, _cts.Token);
                AccessToken = token;
                DialogResult = true;
                Close();
            }
            catch (OperationCanceledException) { /* user canceled */ }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "GitHub Sign-in", MessageBoxButton.OK, MessageBoxImage.Error);
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