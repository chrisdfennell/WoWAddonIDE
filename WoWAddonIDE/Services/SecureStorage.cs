// File: WoWAddonIDE/Services/SecureStorage.cs
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WoWAddonIDE.Services
{
    internal static class SecureStorage
    {
        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WoWAddonIDE");

        private static string PathFor(string key) => System.IO.Path.Combine(Dir, $"{key}.dat");

        public static void SaveString(string key, string value)
        {
            Directory.CreateDirectory(Dir);
            var plain = Encoding.UTF8.GetBytes(value ?? "");
            var cipher = ProtectedData.Protect(plain, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            File.WriteAllBytes(PathFor(key), cipher);
        }

        public static string? LoadString(string key)
        {
            var p = PathFor(key);
            if (!File.Exists(p)) return null;
            var cipher = File.ReadAllBytes(p);
            var plain = ProtectedData.Unprotect(cipher, optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }

        public static void Delete(string key)
        {
            var p = PathFor(key);
            if (File.Exists(p)) File.Delete(p);
        }
    }
}