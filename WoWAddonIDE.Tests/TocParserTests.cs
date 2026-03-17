using System;
using System.IO;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Tests;

public class TocParserTests
{
    [Fact]
    public void GenerateDefaultToc_ContainsInterface()
    {
        var toc = TocParser.GenerateDefaultToc("MyAddon", "110005");
        Assert.Contains("## Interface: 110005", toc);
    }

    [Fact]
    public void GenerateDefaultToc_ContainsTitle()
    {
        var toc = TocParser.GenerateDefaultToc("CoolAddon", "110005");
        Assert.Contains("## Title: CoolAddon", toc);
    }

    [Fact]
    public void GenerateDefaultToc_ContainsMainLua()
    {
        var toc = TocParser.GenerateDefaultToc("MyAddon", "110005");
        Assert.Contains("Main.lua", toc);
    }

    [Fact]
    public void GenerateDefaultToc_ContainsAddonNameLua()
    {
        var toc = TocParser.GenerateDefaultToc("MyAddon", "110005");
        Assert.Contains("MyAddon.lua", toc);
    }

    [Fact]
    public void GenerateDefaultToc_ContainsRequiredKeys()
    {
        var toc = TocParser.GenerateDefaultToc("TestAddon", "100207");
        Assert.Contains("## Interface:", toc);
        Assert.Contains("## Title:", toc);
        Assert.Contains("## Author:", toc);
        Assert.Contains("## Version:", toc);
        Assert.Contains("## Notes:", toc);
    }

    [Fact]
    public void GenerateDefaultToc_EmptyName_StillWorks()
    {
        var toc = TocParser.GenerateDefaultToc("", "110005");
        Assert.Contains("## Interface: 110005", toc);
        Assert.Contains("## Title:", toc);
    }

    [Fact]
    public void GenerateFlavorTocs_CreatesFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"TocTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var baseToc = Path.Combine(dir, "TestAddon.toc");
            File.WriteAllText(baseToc, "## Interface: 110005\n## Title: TestAddon\nMain.lua\n");

            var flavors = new[] { ("Mainline", "110005"), ("Vanilla", "11506") };
            var created = TocParser.GenerateFlavorTocs(dir, "TestAddon", baseToc, flavors);

            Assert.Equal(2, created.Count);
            Assert.True(File.Exists(Path.Combine(dir, "TestAddon_Mainline.toc")));
            Assert.True(File.Exists(Path.Combine(dir, "TestAddon_Vanilla.toc")));

            var vanillaContent = File.ReadAllText(Path.Combine(dir, "TestAddon_Vanilla.toc"));
            Assert.Contains("## Interface: 11506", vanillaContent);
            Assert.Contains("Main.lua", vanillaContent);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void DiscoverTocFiles_FindsBaseAndFlavors()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"TocTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "MyAddon.toc"), "## Interface: 110005\n");
            File.WriteAllText(Path.Combine(dir, "MyAddon_Mainline.toc"), "## Interface: 110005\n");
            File.WriteAllText(Path.Combine(dir, "MyAddon_Vanilla.toc"), "## Interface: 11506\n");

            var found = TocParser.DiscoverTocFiles(dir, "MyAddon");

            Assert.Equal(3, found.Count);
            Assert.Contains(found, f => f.Label == "Base");
            Assert.Contains(found, f => f.Label.Contains("Mainline"));
            Assert.Contains(found, f => f.Label.Contains("Vanilla"));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void GenerateFlavorTocs_SkipsEmptyInterface()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"TocTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var baseToc = Path.Combine(dir, "Test.toc");
            File.WriteAllText(baseToc, "## Interface: 110005\n## Title: Test\n");

            var flavors = new[] { ("Mainline", "110005"), ("Vanilla", "") };
            var created = TocParser.GenerateFlavorTocs(dir, "Test", baseToc, flavors);

            Assert.Single(created);
            Assert.False(File.Exists(Path.Combine(dir, "Test_Vanilla.toc")));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
