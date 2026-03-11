using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace WoWAddonIDE.Services
{
    public static class SymbolService
    {
        public class SymbolLocation
        {
            public string Name { get; set; } = "";
            public string File { get; set; } = "";
            public int Line { get; set; }
            public string Kind { get; set; } = "function";
        }

        // -------- Patterns --------

        // function Foo.Bar:Baz(...)  — supports colon syntax for methods
        private static readonly Regex Fn1 = new(
            @"^\s*function\s+([A-Za-z_]\w*(?:[.:][A-Za-z_]\w*)*)\s*\(",
            RegexOptions.Compiled);

        // local function baz(...)
        private static readonly Regex Fn2 = new(
            @"^\s*local\s+function\s+([A-Za-z_]\w*)\s*\(",
            RegexOptions.Compiled);

        // Foo.Bar = function(...)  or  Foo["bar"] = function(...)
        private static readonly Regex AssignFn = new(
            @"^\s*([A-Za-z_]\w*(?:[.:]\w+|\[""[^""]*""\])*)\s*=\s*function\s*\(",
            RegexOptions.Compiled);

        // local MyTable = {} (useful for Go-to-Definition on table names)
        private static readonly Regex LocalTableDef = new(
            @"^\s*local\s+([A-Za-z_]\w*)\s*=\s*\{",
            RegexOptions.Compiled);

        // Block comment detection
        private static readonly Regex BlockCommentStart = new(@"--\[\[", RegexOptions.Compiled);
        private static readonly Regex BlockCommentEnd = new(@"\]\]", RegexOptions.Compiled);

        public static Dictionary<string, List<SymbolLocation>> BuildIndex(string root)
        {
            var ix = new Dictionary<string, List<SymbolLocation>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return ix;

            foreach (var file in Directory.GetFiles(root, "*.lua", SearchOption.AllDirectories))
            {
                try
                {
                    IndexFile(file, ix);
                }
                catch (Exception ex)
                {
                    LogService.Warn($"SymbolService: failed to index {Path.GetFileName(file)}", ex);
                }
            }
            return ix;
        }

        /// <summary>
        /// Index a single file's symbols into the dictionary.
        /// Public so it can be called incrementally when a file changes.
        /// </summary>
        public static void IndexFile(string file, Dictionary<string, List<SymbolLocation>> ix)
        {
            var lines = File.ReadAllLines(file);
            bool inBlockComment = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Track block comments
                if (inBlockComment)
                {
                    if (BlockCommentEnd.IsMatch(line))
                        inBlockComment = false;
                    continue;
                }

                if (BlockCommentStart.IsMatch(line))
                {
                    inBlockComment = true;
                    continue;
                }

                // Skip comment-only lines
                if (line.TrimStart().StartsWith("--")) continue;

                Match m;
                string? name = null;
                string kind = "function";

                if ((m = Fn1.Match(line)).Success)
                {
                    name = m.Groups[1].Value;
                    kind = name.Contains(':') ? "method" : "function";
                }
                else if ((m = Fn2.Match(line)).Success)
                {
                    name = m.Groups[1].Value;
                    kind = "function (local)";
                }
                else if ((m = AssignFn.Match(line)).Success)
                {
                    name = m.Groups[1].Value;
                    kind = "function (assign)";
                }
                else if ((m = LocalTableDef.Match(line)).Success)
                {
                    name = m.Groups[1].Value;
                    kind = "table";
                }

                if (!string.IsNullOrEmpty(name))
                {
                    if (!ix.TryGetValue(name, out var list))
                        ix[name] = list = new List<SymbolLocation>();
                    list.Add(new SymbolLocation { Name = name, File = file, Line = i + 1, Kind = kind });
                }
            }
        }

        /// <summary>
        /// Re-index a single file (remove old entries, add new ones).
        /// Useful for incremental updates on file save.
        /// </summary>
        public static void ReindexFile(string file, Dictionary<string, List<SymbolLocation>> ix)
        {
            // Remove old entries for this file
            var keysToClean = new List<string>();
            foreach (var kvp in ix)
            {
                kvp.Value.RemoveAll(loc =>
                    string.Equals(loc.File, file, StringComparison.OrdinalIgnoreCase));
                if (kvp.Value.Count == 0) keysToClean.Add(kvp.Key);
            }
            foreach (var key in keysToClean) ix.Remove(key);

            // Re-add
            if (File.Exists(file))
                IndexFile(file, ix);
        }
    }
}
