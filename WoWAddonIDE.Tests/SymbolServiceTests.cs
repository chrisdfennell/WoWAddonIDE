using System.IO;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Tests;

public class SymbolServiceTests
{
    [Fact]
    public void IndexFile_DetectsGlobalFunction()
    {
        var (file, ix) = IndexFromCode("function MyFunc()\nend");
        Assert.True(ix.ContainsKey("MyFunc"));
        Assert.Equal("function", ix["MyFunc"][0].Kind);
        Assert.Equal(1, ix["MyFunc"][0].Line);
    }

    [Fact]
    public void IndexFile_DetectsLocalFunction()
    {
        var (file, ix) = IndexFromCode("local function helper(x)\n  return x\nend");
        Assert.True(ix.ContainsKey("helper"));
        Assert.Equal("function (local)", ix["helper"][0].Kind);
    }

    [Fact]
    public void IndexFile_DetectsAssignedFunction()
    {
        var (file, ix) = IndexFromCode("MyAddon.Init = function()\nend");
        Assert.True(ix.ContainsKey("MyAddon.Init"));
        Assert.Equal("function (assign)", ix["MyAddon.Init"][0].Kind);
    }

    [Fact]
    public void IndexFile_DetectsMethodSyntax()
    {
        var (file, ix) = IndexFromCode("function MyAddon:OnEnable()\nend");
        Assert.True(ix.ContainsKey("MyAddon:OnEnable"));
        Assert.Equal("method", ix["MyAddon:OnEnable"][0].Kind);
    }

    [Fact]
    public void IndexFile_DetectsLocalTable()
    {
        var (file, ix) = IndexFromCode("local defaults = {\n  x = 1\n}");
        Assert.True(ix.ContainsKey("defaults"));
        Assert.Equal("table", ix["defaults"][0].Kind);
    }

    [Fact]
    public void IndexFile_SkipsBlockComments()
    {
        var (file, ix) = IndexFromCode("--[[\nfunction Hidden()\nend\n]]\nfunction Visible()\nend");
        Assert.False(ix.ContainsKey("Hidden"));
        Assert.True(ix.ContainsKey("Visible"));
    }

    [Fact]
    public void IndexFile_SkipsSingleLineComments()
    {
        var (file, ix) = IndexFromCode("-- function NotReal()\nfunction Real()\nend");
        Assert.False(ix.ContainsKey("NotReal"));
        Assert.True(ix.ContainsKey("Real"));
    }

    [Fact]
    public void IndexFile_CaseInsensitiveLookup()
    {
        var (file, ix) = IndexFromCode("function MyFunc()\nend");
        Assert.True(ix.ContainsKey("myfunc"));
        Assert.True(ix.ContainsKey("MYFUNC"));
    }

    [Fact]
    public void ReindexFile_ReplacesOldEntries()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SymbolTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var file = Path.Combine(dir, "test.lua");
            File.WriteAllText(file, "function OldFunc()\nend");

            var ix = SymbolService.BuildIndex(dir);
            Assert.True(ix.ContainsKey("OldFunc"));

            // Simulate edit
            File.WriteAllText(file, "function NewFunc()\nend");
            SymbolService.ReindexFile(file, ix);

            Assert.False(ix.ContainsKey("OldFunc"));
            Assert.True(ix.ContainsKey("NewFunc"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void BuildIndex_EmptyRoot_ReturnsEmpty()
    {
        var ix = SymbolService.BuildIndex("");
        Assert.Empty(ix);
    }

    [Fact]
    public void BuildIndex_NonexistentRoot_ReturnsEmpty()
    {
        var ix = SymbolService.BuildIndex(@"C:\nonexistent_path_xyz");
        Assert.Empty(ix);
    }

    // Helper: create a temp file, index it, return results
    private static (string file, Dictionary<string, List<SymbolService.SymbolLocation>> ix) IndexFromCode(string code)
    {
        var dir = Path.Combine(Path.GetTempPath(), "SymbolTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "test.lua");
        File.WriteAllText(file, code);

        var ix = new Dictionary<string, List<SymbolService.SymbolLocation>>(StringComparer.OrdinalIgnoreCase);
        SymbolService.IndexFile(file, ix);

        // Cleanup
        try { Directory.Delete(dir, true); } catch { }

        return (file, ix);
    }
}
