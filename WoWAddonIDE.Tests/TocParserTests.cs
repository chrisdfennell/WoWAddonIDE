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
}
