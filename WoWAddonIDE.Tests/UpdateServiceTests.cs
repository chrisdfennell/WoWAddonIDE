using WoWAddonIDE.Services;

namespace WoWAddonIDE.Tests;

// Covers the checksum-parsing used to verify auto-update downloads (see UpdateService /
// UpdateAvailableWindow). A wrong parse would either block valid updates or, worse, accept
// an unverified binary, so this is worth pinning down.
public class UpdateServiceTests
{
    private const string Exe = "WoWAddonIDE.exe";
    private const string Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [Fact]
    public void ParseSha256_BareHash()
    {
        Assert.Equal(Hash, UpdateService.ParseSha256(Hash, Exe));
    }

    [Fact]
    public void ParseSha256_HashSpaceFilename()
    {
        Assert.Equal(Hash, UpdateService.ParseSha256($"{Hash}  {Exe}", Exe));
    }

    [Fact]
    public void ParseSha256_BinaryModeAsterisk()
    {
        Assert.Equal(Hash, UpdateService.ParseSha256($"{Hash} *{Exe}", Exe));
    }

    [Fact]
    public void ParseSha256_MultiLine_PicksMatchingFilename()
    {
        var other = "1111111111111111111111111111111111111111111111111111111111111111";
        var content = $"{other}  SomethingElse.exe\n{Hash}  {Exe}\n";
        Assert.Equal(Hash, UpdateService.ParseSha256(content, Exe));
    }

    [Fact]
    public void ParseSha256_UppercaseNormalizedToLower()
    {
        Assert.Equal(Hash, UpdateService.ParseSha256($"{Hash.ToUpperInvariant()}  {Exe}", Exe));
    }

    [Fact]
    public void ParseSha256_NoValidHash_ReturnsNull()
    {
        Assert.Null(UpdateService.ParseSha256("not-a-hash  file.exe", Exe));
        Assert.Null(UpdateService.ParseSha256("", Exe));
    }

    [Fact]
    public void ParseSha256_FilenameMismatchWithNoBareFallback_ReturnsNull()
    {
        Assert.Null(UpdateService.ParseSha256($"{Hash}  OtherApp.exe", Exe));
    }
}
