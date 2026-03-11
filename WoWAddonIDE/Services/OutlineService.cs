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

        // -------- Patterns (ordered by priority) --------

        // function Foo.Bar:Baz(...)
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

        // local x = ... (but not if it's a function)
        private static readonly Regex LocalVar = new(
            @"^\s*local\s+([A-Za-z_]\w*)\b(?!\s*=\s*function)",
            RegexOptions.Compiled);

        // MyTable = { ... (table definition)
        private static readonly Regex TableDef = new(
            @"^\s*([A-Za-z_]\w*)\s*=\s*\{",
            RegexOptions.Compiled);

        // local MyTable = {} (local table)
        private static readonly Regex LocalTableDef = new(
            @"^\s*local\s+([A-Za-z_]\w*)\s*=\s*\{",
            RegexOptions.Compiled);

        // Mixin pattern: MyMixin = CreateFromMixins(...)
        private static readonly Regex MixinDef = new(
            @"^\s*([A-Za-z_]\w*)\s*=\s*CreateFromMixins\s*\(",
            RegexOptions.Compiled);

        // Event handler: frame:SetScript("OnEvent", function(...) or RegisterEvent
        private static readonly Regex EventHandler = new(
            @"^\s*([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*):(?:SetScript|RegisterEvent)\s*\(",
            RegexOptions.Compiled);

        // Comment block markers for region-like folding: -- === SECTION ===
        private static readonly Regex SectionComment = new(
            @"^\s*--\s*[=\-]{3,}\s*(.+?)\s*[=\-]*\s*$",
            RegexOptions.Compiled);

        // Detect lines inside block comments
        private static readonly Regex BlockCommentStart = new(
            @"--\[\[", RegexOptions.Compiled);
        private static readonly Regex BlockCommentEnd = new(
            @"\]\]", RegexOptions.Compiled);

        public static List<OutlineItem> Build(string text)
        {
            var list = new List<OutlineItem>();
            if (string.IsNullOrEmpty(text)) return list;

            var lines = text.Split('\n');
            bool inBlockComment = false;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');

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

                // Skip single-line comments (except section markers)
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("--"))
                {
                    var sm = SectionComment.Match(line);
                    if (sm.Success)
                        list.Add(new OutlineItem { Kind = "section", Name = sm.Groups[1].Value.Trim(), Line = i + 1 });
                    continue;
                }

                Match m;
                if ((m = Fn1.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "function", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = Fn2.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "function (local)", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = AssignFn.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "function (assign)", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = MixinDef.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "mixin", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = LocalTableDef.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "table (local)", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = TableDef.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "table", Name = m.Groups[1].Value, Line = i + 1 });
                else if ((m = LocalVar.Match(line)).Success)
                    list.Add(new OutlineItem { Kind = "local", Name = m.Groups[1].Value, Line = i + 1 });
            }
            return list;
        }
    }
}
