using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WoWAddonIDE.Services
{
    public record GitHubDeviceCode(
        string device_code,
        string user_code,
        string verification_uri,
        int expires_in,
        int interval);

    public static class GitHubAuthService
    {
        static readonly Uri DeviceCodeEndpoint = new("https://github.com/login/device/code");
        static readonly Uri TokenEndpoint = new("https://github.com/login/oauth/access_token");

        public static async Task<GitHubDeviceCode> BeginDeviceFlowAsync(string clientId, string scope, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("GitHub OAuth Client ID is missing. Set it in Git/GitHub Settings.");

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // GitHub expects form-URL-encoded
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = scope
            });

            var resp = await http.PostAsync(DeviceCodeEndpoint, body, ct);
            var text = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                // Surface GitHub’s specific error nicely
                if (text.Contains("device_flow_disabled", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "GitHub says the Device Authorization Flow is disabled for this OAuth App.\n\n" +
                        "Go to GitHub → Settings → Developer settings → OAuth Apps → your app → Edit → " +
                        "enable Device Flow, then try again.");

                throw new InvalidOperationException($"GitHub device code error {(int)resp.StatusCode}: {text}");
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            return new GitHubDeviceCode(
                root.GetProperty("device_code").GetString()!,
                root.GetProperty("user_code").GetString()!,
                root.GetProperty("verification_uri").GetString()!,
                root.GetProperty("expires_in").GetInt32(),
                root.GetProperty("interval").GetInt32()
            );
        }

        public static async Task<string> WaitForAccessTokenAsync(string clientId, string deviceCode, int intervalSeconds, CancellationToken ct)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var form = $"client_id={Uri.EscapeDataString(clientId)}&" +
                       $"device_code={Uri.EscapeDataString(deviceCode)}&" +
                       $"grant_type=urn:ietf:params:oauth:grant-type:device_code";

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var resp = await http.PostAsync(TokenEndpoint,
                    new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded"), ct);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("access_token", out var tokenProp))
                    return tokenProp.GetString()!;

                if (root.TryGetProperty("error", out var err))
                {
                    var error = err.GetString();
                    switch (error)
                    {
                        case "authorization_pending":
                            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);
                            continue;
                        case "slow_down":
                            intervalSeconds += 5;
                            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);
                            continue;
                        case "expired_token":
                            throw new InvalidOperationException("Device code expired. Start sign-in again.");
                        case "access_denied":
                            throw new OperationCanceledException("User denied access.");
                        default:
                            throw new InvalidOperationException("OAuth error: " + error);
                    }
                }

                // Safety backoff
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), ct);
            }
        }
    }
}