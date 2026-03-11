using System;
using System.IO;

namespace WoWAddonIDE.Services
{
    /// <summary>
    /// Structured logging service with severity levels.
    /// Supports an optional UI callback for Output panel display.
    /// </summary>
    public enum LogLevel { Debug, Info, Warning, Error }

    public static class LogService
    {
        /// <summary>
        /// Optional callback to display log messages in the UI Output panel.
        /// Set this from MainWindow on startup.
        /// </summary>
        public static Action<string>? OutputSink { get; set; }

        /// <summary>Minimum severity to log. Messages below this are discarded.</summary>
        public static LogLevel MinLevel { get; set; } = LogLevel.Debug;

        private static readonly object _fileLock = new();

        public static void Debug(string message) => Write(LogLevel.Debug, message);
        public static void Info(string message) => Write(LogLevel.Info, message);
        public static void Warn(string message) => Write(LogLevel.Warning, message);
        public static void Error(string message) => Write(LogLevel.Error, message);

        public static void Warn(string message, Exception ex) =>
            Write(LogLevel.Warning, $"{message}: {ex.Message}");

        public static void Error(string message, Exception ex) =>
            Write(LogLevel.Error, $"{message}: {ex.Message}");

        private static void Write(LogLevel level, string message)
        {
            if (level < MinLevel) return;

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var prefix = level switch
            {
                LogLevel.Debug => "DBG",
                LogLevel.Info => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                _ => "???"
            };

            var formatted = $"[{timestamp}] [{prefix}] {message}";

            // Push to UI sink (if connected)
            try { OutputSink?.Invoke(formatted); }
            catch { /* never fail on UI logging */ }

            // Also write to log file for Warning+ severity
            if (level >= LogLevel.Warning)
                WriteToFile(formatted);
        }

        private static void WriteToFile(string line)
        {
            try
            {
                var dir = Constants.AppDataDir;
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "ide.log");

                lock (_fileLock)
                {
                    // Simple size-based rotation: if > 1MB, archive
                    if (File.Exists(path))
                    {
                        var fi = new FileInfo(path);
                        if (fi.Length > 1_048_576)
                        {
                            var archive = Path.Combine(dir, "ide.log.1");
                            if (File.Exists(archive)) File.Delete(archive);
                            File.Move(path, archive);
                        }
                    }

                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch { /* never fail on file logging */ }
        }
    }
}
