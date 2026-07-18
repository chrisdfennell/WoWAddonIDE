using System;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Tests;

// SecureStorage uses Windows DPAPI (ProtectedData) and persists to
// %AppData%\WoWAddonIDE\<key>.dat. Tests use a unique key and clean up after themselves
// so they never touch a real stored token.
public class SecureStorageTests
{
    private static string NewKey() => $"test_{Guid.NewGuid():N}";

    [Fact]
    public void SaveThenLoad_RoundTripsValue()
    {
        var key = NewKey();
        try
        {
            SecureStorage.SaveString(key, "ghp_secretTokenValue_123");
            Assert.Equal("ghp_secretTokenValue_123", SecureStorage.LoadString(key));
        }
        finally { SecureStorage.Delete(key); }
    }

    [Fact]
    public void Load_MissingKey_ReturnsNull()
    {
        Assert.Null(SecureStorage.LoadString(NewKey()));
    }

    [Fact]
    public void Save_OverwritesExistingValue()
    {
        var key = NewKey();
        try
        {
            SecureStorage.SaveString(key, "first");
            SecureStorage.SaveString(key, "second");
            Assert.Equal("second", SecureStorage.LoadString(key));
        }
        finally { SecureStorage.Delete(key); }
    }

    [Fact]
    public void Delete_RemovesValue()
    {
        var key = NewKey();
        SecureStorage.SaveString(key, "value");
        SecureStorage.Delete(key);
        Assert.Null(SecureStorage.LoadString(key));
    }

    [Fact]
    public void Delete_MissingKey_DoesNotThrow()
    {
        var ex = Record.Exception(() => SecureStorage.Delete(NewKey()));
        Assert.Null(ex);
    }
}
