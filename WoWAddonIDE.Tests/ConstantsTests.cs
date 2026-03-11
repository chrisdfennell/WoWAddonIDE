using System.IO;

namespace WoWAddonIDE.Tests;

public class ConstantsTests
{
    [Fact]
    public void AppDataDir_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(Constants.AppDataDir));
    }

    [Fact]
    public void SettingsPath_ContainsSettingsJson()
    {
        Assert.EndsWith("settings.json", Constants.SettingsPath);
    }

    [Fact]
    public void WatchedExtensions_ContainsLua()
    {
        Assert.Contains(".lua", Constants.WatchedExtensions);
    }

    [Fact]
    public void HiddenFolders_ContainsGit()
    {
        Assert.Contains(".git", Constants.HiddenFolders);
    }

    [Fact]
    public void DefaultPackageExcludes_ContainsCommonExcludes()
    {
        Assert.Contains(".psd", Constants.DefaultPackageExcludes);
        Assert.Contains(".git", Constants.DefaultPackageExcludes);
        Assert.Contains("obj", Constants.DefaultPackageExcludes);
    }
}
