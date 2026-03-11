using System.IO;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Tests;

public class LuaLintTests
{
    [Fact]
    public void Pass_ValidLua_NoErrors()
    {
        var (project, cleanup) = CreateProject("local x = 1\nprint(x)");
        try
        {
            var results = LuaLint.Pass(project);
            Assert.Single(results);
            Assert.Contains("No issues found", results[0]);
        }
        finally { cleanup(); }
    }

    [Fact]
    public void Pass_SyntaxError_ReportsError()
    {
        var (project, cleanup) = CreateProject("function broken(\nend"); // missing closing paren
        try
        {
            var results = LuaLint.Pass(project);
            Assert.True(results.Count > 0);
            Assert.Contains(results, r => r.Contains("LINT"));
        }
        finally { cleanup(); }
    }

    [Fact]
    public void Analyze_TrailingWhitespace_ReportsInfo()
    {
        var (project, cleanup) = CreateProject("local x = 1   \nprint(x)");
        try
        {
            var diags = LuaLint.Analyze(project);
            Assert.Contains(diags, d => d.Severity == "info" && d.Message.Contains("Trailing whitespace"));
        }
        finally { cleanup(); }
    }

    [Fact]
    public void Analyze_UnbalancedParens_ReportsError()
    {
        var (project, cleanup) = CreateProject("print(x\n-- missing close paren");
        try
        {
            var diags = LuaLint.Analyze(project);
            Assert.Contains(diags, d => d.Severity == "error" && d.Message.Contains("parenthes"));
        }
        finally { cleanup(); }
    }

    [Fact]
    public void Analyze_UnbalancedBraces_ReportsError()
    {
        var (project, cleanup) = CreateProject("local t = {\n  x = 1\n-- missing close brace");
        try
        {
            var diags = LuaLint.Analyze(project);
            Assert.Contains(diags, d => d.Severity == "error" && d.Message.Contains("brace"));
        }
        finally { cleanup(); }
    }

    [Fact]
    public void Analyze_DeprecatedApi_ReportsWarning()
    {
        var (project, cleanup) = CreateProject("local name = GetSpellInfo(12345)");
        try
        {
            var diags = LuaLint.Analyze(project);
            Assert.Contains(diags, d => d.Severity == "warning" && d.Message.Contains("deprecated"));
        }
        finally { cleanup(); }
    }

    [Fact]
    public void Analyze_EmptyProject_NoFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LintTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var tocPath = Path.Combine(dir, "Test.toc");
        File.WriteAllText(tocPath, "## Interface: 110005\n## Title: Test");

        var project = new AddonProject
        {
            Name = "Test",
            RootPath = dir,
            TocPath = tocPath,
            Files = new List<string> { tocPath } // no .lua files
        };

        try
        {
            var diags = LuaLint.Analyze(project);
            Assert.Empty(diags); // no lua files to lint
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Analyze_SkipsNonLuaFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "LintTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var xmlFile = Path.Combine(dir, "frame.xml");
        File.WriteAllText(xmlFile, "<Ui></Ui>");

        var project = new AddonProject
        {
            Name = "Test",
            RootPath = dir,
            TocPath = Path.Combine(dir, "Test.toc"),
            Files = new List<string> { xmlFile }
        };

        try
        {
            var diags = LuaLint.Analyze(project);
            Assert.Empty(diags);
        }
        finally { Directory.Delete(dir, true); }
    }

    // Helper
    private static (AddonProject project, Action cleanup) CreateProject(string luaCode)
    {
        var dir = Path.Combine(Path.GetTempPath(), "LintTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var luaFile = Path.Combine(dir, "Main.lua");
        File.WriteAllText(luaFile, luaCode);
        var tocPath = Path.Combine(dir, "Test.toc");
        File.WriteAllText(tocPath, "## Interface: 110005\n## Title: Test\nMain.lua");

        var project = new AddonProject
        {
            Name = "Test",
            RootPath = dir,
            TocPath = tocPath,
            Files = new List<string> { luaFile, tocPath }
        };

        return (project, () => { try { Directory.Delete(dir, true); } catch { } });
    }
}
