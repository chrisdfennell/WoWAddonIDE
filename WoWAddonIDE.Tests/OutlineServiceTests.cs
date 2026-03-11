using WoWAddonIDE.Services;

namespace WoWAddonIDE.Tests;

public class OutlineServiceTests
{
    [Fact]
    public void Build_DetectsGlobalFunction()
    {
        var text = "function MyAddon_OnLoad(self)\n  print('hi')\nend";
        var items = OutlineService.Build(text);
        Assert.Single(items);
        Assert.Equal("function", items[0].Kind);
        Assert.Equal("MyAddon_OnLoad", items[0].Name);
        Assert.Equal(1, items[0].Line);
    }

    [Fact]
    public void Build_DetectsLocalFunction()
    {
        var text = "local function helper()\n  return 1\nend";
        var items = OutlineService.Build(text);
        Assert.Single(items);
        Assert.Equal("function (local)", items[0].Kind);
        Assert.Equal("helper", items[0].Name);
    }

    [Fact]
    public void Build_DetectsAssignedFunction()
    {
        var text = "MyAddon.OnEvent = function(self, event)\nend";
        var items = OutlineService.Build(text);
        Assert.Single(items);
        Assert.Equal("function (assign)", items[0].Kind);
        Assert.Equal("MyAddon.OnEvent", items[0].Name);
    }

    [Fact]
    public void Build_DetectsMethodSyntax()
    {
        var text = "function MyAddon:OnEnable()\n  -- do stuff\nend";
        var items = OutlineService.Build(text);
        Assert.Single(items);
        Assert.Equal("function", items[0].Kind);
        Assert.Equal("MyAddon:OnEnable", items[0].Name);
    }

    [Fact]
    public void Build_DetectsTable()
    {
        var text = "defaults = {\n  health = 100\n}";
        var items = OutlineService.Build(text);
        Assert.Single(items);
        Assert.Equal("table", items[0].Kind);
        Assert.Equal("defaults", items[0].Name);
    }

    [Fact]
    public void Build_DetectsLocalTable()
    {
        var text = "local config = {\n  debug = true\n}";
        var items = OutlineService.Build(text);
        Assert.Single(items);
        Assert.Equal("table (local)", items[0].Kind);
        Assert.Equal("config", items[0].Name);
    }

    [Fact]
    public void Build_DetectsLocalVariable()
    {
        var text = "local addonName = 'MyAddon'";
        var items = OutlineService.Build(text);
        Assert.Single(items);
        Assert.Equal("local", items[0].Kind);
        Assert.Equal("addonName", items[0].Name);
    }

    [Fact]
    public void Build_DetectsSectionComment()
    {
        var text = "-- === Events ===\nfunction OnEvent()\nend";
        var items = OutlineService.Build(text);
        Assert.Equal(2, items.Count);
        Assert.Equal("section", items[0].Kind);
        Assert.Equal("Events", items[0].Name);
    }

    [Fact]
    public void Build_SkipsBlockComments()
    {
        var text = "--[[\nfunction HiddenFunction()\nend\n]]\nfunction RealFunction()\nend";
        var items = OutlineService.Build(text);
        Assert.Single(items);
        Assert.Equal("RealFunction", items[0].Name);
    }

    [Fact]
    public void Build_SkipsSingleLineComments()
    {
        var text = "-- function NotAFunction()\nfunction RealFunction()\nend";
        var items = OutlineService.Build(text);
        Assert.Single(items);
        Assert.Equal("RealFunction", items[0].Name);
    }

    [Fact]
    public void Build_EmptyText_ReturnsEmptyList()
    {
        Assert.Empty(OutlineService.Build(""));
        Assert.Empty(OutlineService.Build(null!));
    }

    [Fact]
    public void Build_MultipleItems_CorrectLineNumbers()
    {
        var text = "local x = 1\nfunction Foo()\nend\nlocal function Bar()\nend";
        var items = OutlineService.Build(text);
        Assert.Equal(3, items.Count);
        Assert.Equal(1, items[0].Line);
        Assert.Equal(2, items[1].Line);
        Assert.Equal(4, items[2].Line);
    }
}
