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
        }

        // Patterns: function Foo.Bar( ... ), local function baz( ... ), Foo.Bar = function(
        static readonly Regex Fn1 = new(@"^\s*function\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*\(", RegexOptions.Compiled);
        static readonly Regex Fn2 = new(@"^\s*local\s+function\s+([A-Za-z_]\w*)\s*\(", RegexOptions.Compiled);
        static readonly Regex AssignFn = new(@"^\s*([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*=\s*function\s*\(", RegexOptions.Compiled);

        public static Dictionary<string, List<SymbolLocation>> BuildIndex(string root)
        {
            var ix = new Dictionary<string, List<SymbolLocation>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return ix;

            foreach (var file in Directory.GetFiles(root, "*.lua", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    Match m;
                    string? name = null;
                    if ((m = Fn1.Match(line)).Success) name = m.Groups[1].Value;
                    else if ((m = Fn2.Match(line)).Success) name = m.Groups[1].Value;
                    else if ((m = AssignFn.Match(line)).Success) name = m.Groups[1].Value;

                    if (!string.IsNullOrEmpty(name))
                    {
                        if (!ix.TryGetValue(name, out var list))
                            ix[name] = list = new List<SymbolLocation>();
                        list.Add(new SymbolLocation { Name = name, File = file, Line = i + 1 });
                    }
                }
            }
            return ix;
        }
    }
}