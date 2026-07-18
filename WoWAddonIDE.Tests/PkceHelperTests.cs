using System;
using System.Security.Cryptography;
using System.Text;
using WoWAddonIDE.Services.OAuth;

namespace WoWAddonIDE.Tests;

public class PkceHelperTests
{
    private static bool IsBase64Url(string s)
    {
        foreach (var c in s)
        {
            bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') ||
                      (c >= '0' && c <= '9') || c == '-' || c == '_';
            if (!ok) return false;
        }
        return s.Length > 0;
    }

    [Fact]
    public void CreatePkcePair_VerifierIsUrlSafeAndWithinRfcLength()
    {
        var (verifier, _) = PkceHelper.CreatePkcePair();

        Assert.True(IsBase64Url(verifier), "verifier must be URL-safe base64 (no +, /, =)");
        // RFC 7636 requires 43..128 characters.
        Assert.InRange(verifier.Length, 43, 128);
    }

    [Fact]
    public void CreatePkcePair_ChallengeIsS256OfVerifier()
    {
        var (verifier, challenge) = PkceHelper.CreatePkcePair();

        // Independently recompute BASE64URL(SHA256(ASCII(verifier))) and compare.
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var expected = Convert.ToBase64String(hash)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

        Assert.Equal(expected, challenge);
        Assert.True(IsBase64Url(challenge));
    }

    [Fact]
    public void CreatePkcePair_ProducesUniqueVerifiers()
    {
        var (v1, _) = PkceHelper.CreatePkcePair();
        var (v2, _) = PkceHelper.CreatePkcePair();
        Assert.NotEqual(v1, v2);
    }

    [Fact]
    public void CreateState_IsUrlSafeAndUnique()
    {
        var s1 = PkceHelper.CreateState();
        var s2 = PkceHelper.CreateState();

        Assert.True(IsBase64Url(s1));
        Assert.NotEqual(s1, s2);
    }
}
