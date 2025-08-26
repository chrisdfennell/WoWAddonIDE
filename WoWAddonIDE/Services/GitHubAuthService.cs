// File: WoWAddonIDE/Services/GitHubAuthService.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
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

        // ---- Public entry point --------------------------------------------------

        /// <summary>
        /// Runs a full GitHub OAuth (Authorization Code + PKCE) flow.
        /// </summary>
        /// <param name="clientId">OAuth App client_id.</param>
        /// <param name="clientSecret">
        /// Optional. Leave empty when you want a “public client” style flow (PKCE without secret).
        /// If you provide it, GitHub will accept it too.
        /// </param>
        /// <param name="redirect">Must EXACTLY match your OAuth App's callback URL.</param>
        /// <param name="scope">Scopes, e.g. "repo read:user".</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>access_token or null if user cancelled/failed.</returns>
        public static async Task<string?> SignInWithPkceAsync(
            string clientId,
            string? clientSecret,
            string redirect,
            string scope,
            CancellationToken ct)
        {
            redirect = NormalizeRedirect(redirect);

            // 1) Make state + PKCE pair
            string state = NewState();
            var (verifier, challenge) = MakePkcePair();

            // 2) Start listener on the exact redirect (plus 127.0.0.1 convenience)
            using var listener = new HttpListener();
            AddListenerPrefixes(listener, redirect);
            try { listener.Start(); }
            catch (HttpListenerException)
            {
                // If another process grabbed it, bail gracefully
                return null;
            }

            // 3) Open browser
            var authorizeUrl =
                $"{AuthorizeEndpoint}" +
                $"?client_id={Uri.EscapeDataString(clientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirect)}" +
                $"&scope={Uri.EscapeDataString(scope ?? "")}" +
                $"&state={Uri.EscapeDataString(state)}" +
                $"&code_challenge={Uri.EscapeDataString(challenge)}" +
                $"&code_challenge_method=S256" +
                $"&allow_signup=false";

            Process.Start(new ProcessStartInfo { FileName = authorizeUrl, UseShellExecute = true });

            // 4) Wait for callback
            var ctx = await WaitForContextAsync(listener, ct);
            if (ctx == null) return null;

            try
            {
                var query = ctx.Request.Url?.Query ?? "";
                var parsed = System.Web.HttpUtility.ParseQueryString(query);

                var code = parsed.Get("code");
                var st = parsed.Get("state");

                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(st) || st != state)
                {
                    await WriteHtml(ctx, 400,
                        "<h2>Sign-in failed</h2><p>Missing or invalid code/state.</p>");
                    return null;
                }

                // 5) Exchange for token — THIS includes code_verifier ✅
                var token = await ExchangeCodeForTokenAsync(clientId, clientSecret, redirect, code, verifier, ct);
                if (string.IsNullOrWhiteSpace(token))
                {
                    await WriteHtml(ctx, 500,
                        "<h2>Sign-in failed</h2><p>GitHub did not return an access_token.</p>");
                    return null;
                }

                await WriteHtml(ctx, 200,
                    "<h2>Signed in</h2><p>You can close this tab and return to WoWAddonIDE.</p>");
                return token;
            }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }

        // ---- Internals ----------------------------------------------------------

        private static async Task<string?> ExchangeCodeForTokenAsync(
            string clientId,
            string? clientSecret,
            string redirect,
            string code,
            string codeVerifier,
            CancellationToken ct)
        {
            using var http = new HttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint);
            req.Headers.Accept.ParseAdd("application/json");

            // Build form — include code_verifier for PKCE
            var form = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = redirect,
                ["code_verifier"] = codeVerifier
            };

            // Optional secret (OK to omit for PKCE “public client”)
            if (!string.IsNullOrWhiteSpace(clientSecret))
                form["client_secret"] = clientSecret;

            req.Content = new FormUrlEncodedContent(form);

            using var resp = await http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                // Try to surface an error message if present
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("error_description", out var d))
                        throw new InvalidOperationException(d.GetString());
                    if (doc.RootElement.TryGetProperty("error", out var e))
                        throw new InvalidOperationException(e.GetString());
                }
                catch { /* fall through */ }

                throw new InvalidOperationException($"GitHub token error: HTTP {(int)resp.StatusCode}.");
            }

            // { "access_token":"...", "token_type":"bearer", ... }
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("access_token", out var tok))
                    return tok.GetString();
            }
            catch
            {
                // ignore parse errors
            }

            return null;
        }

        private static string NormalizeRedirect(string redirect)
        {
            var r = (redirect ?? "").Trim();
            if (!r.EndsWith("/")) r += "/";
            return r;
        }

        private static void AddListenerPrefixes(HttpListener listener, string redirect)
        {
            var u = new Uri(redirect);
            var path = u.AbsolutePath.Trim('/'); // HttpListener needs trailing slash after path
            listener.Prefixes.Add($"http://localhost:{u.Port}/{path}/");
            listener.Prefixes.Add($"http://127.0.0.1:{u.Port}/{path}/");
        }

        private static async Task<HttpListenerContext?> WaitForContextAsync(HttpListener listener, CancellationToken ct)
        {
            var t = listener.GetContextAsync();
            using (ct.Register(() => { try { listener.Stop(); } catch { } }))
            {
                try { return await t.ConfigureAwait(false); }
                catch { return null; }
            }
        }

        private static async Task WriteHtml(HttpListenerContext ctx, int statusCode, string html)
        {
            var bytes = Encoding.UTF8.GetBytes($"<!doctype html><html><body>{html}</body></html>");
            ctx.Response.StatusCode = statusCode;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = bytes.LongLength;
            await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        private static string NewState()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        // Minimal PKCE helpers (you also have PkceHelper.cs; feel free to use that instead)
        private static (string Verifier, string Challenge) MakePkcePair()
        {
            // Verifier: 43-128 chars, unpadded URL-safe base64
            var bytes = RandomNumberGenerator.GetBytes(32);
            var verifier = Base64Url(bytes);

            // Challenge = BASE64URL( SHA256(verifier) )
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
            var challenge = Base64Url(hash);

            return (verifier, challenge);
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
