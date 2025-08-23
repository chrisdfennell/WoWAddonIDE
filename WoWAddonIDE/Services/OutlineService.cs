using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WoWAddonIDE.Services
{
    public static class OutlineService
    {
        public class OutlineItem
        {
            public string Kind { get; set; } = "";
            public string Name { get; set; } = "";
            public int Line { get; set; }
            public override string ToString() => $"{Kind}: {Name}  (L{Line})";
        }

        // Simple patterns (fast, not a full parser)
        private static readonly Regex Fn1 = new(@"^\s*function\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*\(", RegexOptions.Compiled);
        private static readonly Regex Fn2 = new(@"^\s*local\s+function\s+([A-Za-z_]\w*)\s*\(", RegexOptions.Compiled);
        private static readonly Regex AssignFn = new(@"^\s*([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*=\s*function\s*\(", RegexOptions.Compiled);
        private static readonly Regex LocalVar = new(@"^\s*local\s+([A-Za-z_]\w*)\b(?!\s*=\s*function)", RegexOptions.Compiled);
        private static readonly Regex TableDef = new(@"^\s*([A-Za-z_]\w*)\s*=\s*\{\s*$", RegexOptions.Compiled);

        public static List<OutlineItem> Build(string text)
        {
            var list = new List<OutlineItem>();
            var lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                Match m;
                if ((m = Fn1.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "function", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = Fn2.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "function (local)", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = AssignFn.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "function (assign)", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = TableDef.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "table", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = LocalVar.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "local", Name = m.Groups[1].Value, Line = i + 1 });
            }
            return list;
        }
    }
}