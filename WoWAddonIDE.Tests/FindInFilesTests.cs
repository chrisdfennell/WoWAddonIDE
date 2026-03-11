using System.IO;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Tests;

public class FindInFilesTests
{
    [Fact]
    public async Task SearchAsync_FindsLiteralMatch()
    {
        var dir = CreateTestDir("local foo = 'hello'\nlocal bar = 'world'");
        try
        {
            var hits = await FindInFiles.SearchAsync(dir, "foo", regex: false, caseSensitive: true);
            Assert.Single(hits);
            Assert.Equal(1, hits[0].Line);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SearchAsync_CaseInsensitive()
    {
        var dir = CreateTestDir("local FOO = 1\nlocal foo = 2");
        try
        {
            var hits = await FindInFiles.SearchAsync(dir, "foo", regex: false, caseSensitive: false);
            Assert.Equal(2, hits.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SearchAsync_CaseSensitive()
    {
        var dir = CreateTestDir("local FOO = 1\nlocal foo = 2");
        try
        {
            var hits = await FindInFiles.SearchAsync(dir, "foo", regex: false, caseSensitive: true);
            Assert.Single(hits);
            Assert.Equal(2, hits[0].Line);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SearchAsync_RegexMatch()
    {
        var dir = CreateTestDir("function MyFunc()\nend\nfunction OtherFunc()\nend");
        try
        {
            var hits = await FindInFiles.SearchAsync(dir, @"function\s+\w+Func", regex: true, caseSensitive: true);
            Assert.Equal(2, hits.Count);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ReturnsEmpty()
    {
        var dir = CreateTestDir("local x = 1");
        try
        {
            var hits = await FindInFiles.SearchAsync(dir, "nonexistent", regex: false, caseSensitive: true);
            Assert.Empty(hits);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SearchAsync_EmptyPattern_ReturnsEmpty()
    {
        var dir = CreateTestDir("local x = 1");
        try
        {
            var hits = await FindInFiles.SearchAsync(dir, "", regex: false, caseSensitive: true);
            Assert.Empty(hits);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SearchAsync_EmptyRoot_ReturnsEmpty()
    {
        var hits = await FindInFiles.SearchAsync("", "test", regex: false, caseSensitive: true);
        Assert.Empty(hits);
    }

    [Fact]
    public async Task SearchAsync_WithFilter_OnlyMatchesFilteredFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "FIF_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.lua"), "local findme = 1");
        File.WriteAllText(Path.Combine(dir, "b.xml"), "<findme />");

        try
        {
            var hits = await FindInFiles.SearchAsync(dir, "findme", regex: false, caseSensitive: true, filters: new[] { ".lua" });
            Assert.Single(hits);
            Assert.EndsWith(".lua", hits[0].File);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task SearchAsync_MultipleOccurrencesOnSameLine()
    {
        var dir = CreateTestDir("local x = foo + foo + foo");
        try
        {
            var hits = await FindInFiles.SearchAsync(dir, "foo", regex: false, caseSensitive: true);
            Assert.Equal(3, hits.Count);
            Assert.All(hits, h => Assert.Equal(1, h.Line));
        }
        finally { Directory.Delete(dir, true); }
    }

    private static string CreateTestDir(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "FIF_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "test.lua"), content);
        return dir;
    }
}
