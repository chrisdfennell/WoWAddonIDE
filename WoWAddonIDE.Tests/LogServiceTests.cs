using System.IO;
using WoWAddonIDE.Services;

namespace WoWAddonIDE.Tests;

public class LogServiceTests
{
    [Fact]
    public void OutputSink_ReceivesMessages()
    {
        var messages = new List<string>();
        var oldSink = LogService.OutputSink;
        var oldLevel = LogService.MinLevel;
        try
        {
            LogService.OutputSink = msg => messages.Add(msg);
            LogService.MinLevel = LogLevel.Debug;

            LogService.Info("test message");

            Assert.Single(messages);
            Assert.Contains("INF", messages[0]);
            Assert.Contains("test message", messages[0]);
        }
        finally
        {
            LogService.OutputSink = oldSink;
            LogService.MinLevel = oldLevel;
        }
    }

    [Fact]
    public void MinLevel_FiltersLowerLevels()
    {
        var messages = new List<string>();
        var oldSink = LogService.OutputSink;
        var oldLevel = LogService.MinLevel;
        try
        {
            LogService.OutputSink = msg => messages.Add(msg);
            LogService.MinLevel = LogLevel.Warning;

            LogService.Debug("should not appear");
            LogService.Info("should not appear");
            LogService.Warn("should appear");

            Assert.Single(messages);
            Assert.Contains("WRN", messages[0]);
        }
        finally
        {
            LogService.OutputSink = oldSink;
            LogService.MinLevel = oldLevel;
        }
    }

    [Fact]
    public void Error_WithException_IncludesExceptionMessage()
    {
        var messages = new List<string>();
        var oldSink = LogService.OutputSink;
        var oldLevel = LogService.MinLevel;
        try
        {
            LogService.OutputSink = msg => messages.Add(msg);
            LogService.MinLevel = LogLevel.Debug;

            LogService.Error("operation failed", new InvalidOperationException("bad state"));

            Assert.Single(messages);
            Assert.Contains("ERR", messages[0]);
            Assert.Contains("bad state", messages[0]);
        }
        finally
        {
            LogService.OutputSink = oldSink;
            LogService.MinLevel = oldLevel;
        }
    }

    [Fact]
    public void Warn_WithException_IncludesExceptionMessage()
    {
        var messages = new List<string>();
        var oldSink = LogService.OutputSink;
        var oldLevel = LogService.MinLevel;
        try
        {
            LogService.OutputSink = msg => messages.Add(msg);
            LogService.MinLevel = LogLevel.Debug;

            LogService.Warn("something off", new IOException("disk full"));

            Assert.Single(messages);
            Assert.Contains("WRN", messages[0]);
            Assert.Contains("disk full", messages[0]);
        }
        finally
        {
            LogService.OutputSink = oldSink;
            LogService.MinLevel = oldLevel;
        }
    }
}
