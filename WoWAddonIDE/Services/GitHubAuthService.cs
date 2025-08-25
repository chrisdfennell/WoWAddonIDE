// File: WoWAddonIDE/Services/GitHubAuthService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WoWAddonIDE.Services
{
    public static class GitHubAuthService
    {
        private const string AuthorizeEndpoint = "https://github.com/login/oauth/authorize";
        private const string TokenEndpoint = "https://github.com/login/oauth/access_token";

        // --- helpers ---------------------------------------------------------

        // Base64Url encoding (no padding)
        private static string Base64Url(byte[] data)
        {
            return Convert.ToBase64String(data)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private static (string Verifier, string Challenge) MakePkcePair()
        {
            var bytes = RandomNumberGenerator.GetBytes(64); // 64 bytes -> ~86 chars
            var verifier = Base64Url(bytes);
            using var sha = SHA256.Create();
            var challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
            return (verifier, challenge);
        }

        private static string NewState() => Base64Url(RandomNumberGenerator.GetBytes(32));

        // --- main entry: browser sign-in with PKCE ---------------------------

        /// <summary>
        /// Authorization Code + PKCE. Opens the browser and listens on http://127.0.0.1:{port}/callback.
        /// Configure your GitHub OAuth App “Authorization callback URL” to that exact URL.
        /// </summary>
        public static async Task<string> SignInViaPkceAsync(
            string clientId,
            string? clientSecret,               // optional; include for OAuth Apps
            string scope = "repo read:user",
            int port = 48123,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("GitHub OAuth Client ID is empty.");

            var (verifier, challenge) = MakePkcePair();
            var state = NewState();
            var redirect = $"http://127.0.0.1:{port}/callback";

            var authUrl =
                $"{AuthorizeEndpoint}" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirect)}" +
                $"&scope={Uri.EscapeDataString(scope)}" +
                $"&state={Uri.EscapeDataString(state)}" +
                $"&code_challenge={Uri.EscapeDataString(challenge)}" +
                $"&code_challenge_method=S256";

            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/callback/");
            try { listener.Start(); }
            catch (HttpListenerException ex)
            {
                throw new InvalidOperationException(
                    $"Couldn't start loopback listener on http://127.0.0.1:{port}/callback/.\n" +
                    $"Try a different port or add a URL ACL.\n\n{ex.Message}", ex);
            }

            // Open the browser
            Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

            // Default page so the 'finally' can always reply
            string htmlResponse =
                "<html><body style='font-family:Segoe UI, sans-serif'>" +
                "<h2>You can close this tab</h2><p>Return to WoWAddonIDE.</p></body></html>";

            HttpListenerContext? ctx = null;

            try
            {
                // Cancellation-friendly wait without relying on Task.WaitAsync
                var ctxTask = listener.GetContextAsync();
                var done = await Task.WhenAny(ctxTask, Task.Delay(Timeout.Infinite, ct));
                if (done != ctxTask) throw new OperationCanceledException(ct);
                ctx = await ctxTask;

                var query = ctx.Request.QueryString;
                var code = query["code"];
                var gotState = query["state"];

                if (string.IsNullOrWhiteSpace(code))
                    throw new InvalidOperationException("GitHub did not return an authorization code.");
                if (!string.Equals(gotState, state, StringComparison.Ordinal))
                    throw new InvalidOperationException("OAuth state mismatch.");

                // Exchange code for token (include secret for OAuth Apps)
                var token = await ExchangeCodeForTokenAsync(clientId, clientSecret, code, verifier, redirect, ct);

                htmlResponse =
                    "<html><body style='font-family:Segoe UI, sans-serif'>" +
                    "<h2>GitHub sign-in complete</h2><p>You can close this tab and return to WoWAddonIDE.</p></body></html>";

                return token;
            }
            catch (OperationCanceledException)
            {
                htmlResponse =
                    "<html><body style='font-family:Segoe UI, sans-serif'>" +
                    "<h2>Cancelled</h2><p>You can close this tab.</p></body></html>";
                throw;
            }
            catch (Exception ex)
            {
                htmlResponse =
                    "<html><body style='font-family:Segoe UI, sans-serif'>" +
                    "<h2>Sign-in failed</h2><p>" + WebUtility.HtmlEncode(ex.Message) + "</p></body></html>";
                throw;
            }
            finally
            {
                try
                {
                    if (listener.IsListening && ctx != null)
                    {
                        var buf = Encoding.UTF8.GetBytes(htmlResponse);
                        ctx.Response.ContentType = "text/html; charset=utf-8";
                        ctx.Response.ContentLength64 = buf.Length;
                        await ctx.Response.OutputStream.WriteAsync(buf, 0, buf.Length, ct);
                        ctx.Response.OutputStream.Close();
                    }
                }
                catch { /* ignore */ }
                try { listener.Stop(); } catch { }
            }
        }

        // --- token exchange --------------------------------------------------

        private static async Task<string> ExchangeCodeForTokenAsync(
            string clientId,
            string? clientSecret,   // may be null (GitHub device flow / GitHub App would differ)
            string code,
            string codeVerifier,
            string redirectUri,
            CancellationToken ct)
        {
            using var http = new HttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Headers.UserAgent.ParseAdd("WoWAddonIDE/1.0");

            var form = new List<KeyValuePair<string, string>>
            {
                new("client_id", clientId),
                new("code", code),
                new("redirect_uri", redirectUri),
                new("grant_type", "authorization_code"),
                new("code_verifier", codeVerifier),
            };
            if (!string.IsNullOrWhiteSpace(clientSecret))
                form.Add(new("client_secret", clientSecret)); // OAuth Apps generally expect this

            req.Content = new FormUrlEncodedContent(form);

            using var resp = await http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Token endpoint HTTP {(int)resp.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var err))
            {
                var desc = root.TryGetProperty("error_description", out var d) ? d.GetString() : null;
                throw new InvalidOperationException($"GitHub token error: {err.GetString()} - {desc}");
            }

            if (root.TryGetProperty("access_token", out var tokProp))
            {
                var token = tokProp.GetString();
                if (!string.IsNullOrWhiteSpace(token)) return token!;
            }

            throw new InvalidOperationException("GitHub did not return an access_token.");
        }
    }
}