// File: WoWAddonIDE/Services/OAuth/PkceHelper.cs
using System;
using System.Security.Cryptography;
using System.Text;

namespace WoWAddonIDE.Services.OAuth
{
    internal static class PkceHelper
    {
        public static (string CodeVerifier, string CodeChallenge) CreatePkcePair()
        {
            // high-entropy random verifier (43-128 chars; we’ll use 64)
            var bytes = RandomNumberGenerator.GetBytes(64);
            var verifier = Base64UrlEncode(bytes);
            var challenge = Base64UrlEncode(Sha256(verifier));
            return (verifier, challenge);
        }

        public static string CreateState()
        {
            return Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        }

        private static byte[] Sha256(string input)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.ASCII.GetBytes(input));
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
