using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace WoWAddonIDE.Services
{
    public class LuaDiagnosticsTransformer : DocumentColorizingTransformer
    {
        static readonly Regex Tabs = new(@"\t", RegexOptions.Compiled);
        static readonly Regex Trailing = new(@"[ \t]+(?=\r?$)", RegexOptions.Compiled);

        // very rough global capture: NAME = something at column 0
        static readonly Regex GlobalAssign = new(@"^[A-Za-z_]\w*\s*=", RegexOptions.Compiled);

        private HashSet<string> _globals = new(StringComparer.OrdinalIgnoreCase);

        public void Reanalyze(string text)
        {
            _globals.Clear();
            var lines = text.Split('\n');
            foreach (var raw in lines)
            {
                var line = raw.TrimEnd('\r');
                var m = GlobalAssign.Match(line);
                if (m.Success)
                {
                    var name = m.Value[..m.Value.IndexOf('=')].Trim();
                    if (!_globals.Add(name))
                    {
                        // duplicate; keep set as-is (we'll mark lines during colorize pass)
                    }
                }
            }
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            var text = CurrentContext.Document.GetText(line.Offset, line.Length);
            foreach (Match m in Tabs.Matches(text))
                Underline(line.Offset + m.Index, m.Length, Colors.Orange);

            foreach (Match m in Trailing.Matches(text))
                Underline(line.Offset + m.Index, m.Length, Colors.IndianRed);

            // crude duplicate global detection
            var gm = GlobalAssign.Match(text);
            if (gm.Success)
            {
                var name = gm.Value[..gm.Value.IndexOf('=')].Trim();
                // If more than one occurrence in doc, underline all assigns
                int count = 0;
                foreach (var g in _globals) { if (string.Equals(g, name, StringComparison.OrdinalIgnoreCase)) count++; }
                if (count > 1)
                    Underline(line.Offset + gm.Index, gm.Length, Colors.Gold);
            }
        }

        private void Underline(int startOffset, int length, Color color)
        {
            ChangeLinePart(startOffset, startOffset + length, (el) =>
            {
                el.TextRunProperties.SetForegroundBrush(new SolidColorBrush(color));
                var deco = el.TextRunProperties.TextDecorations ?? new TextDecorationCollection();
                deco = deco.Clone();
                deco.Add(TextDecorations.Underline[0]);
                el.TextRunProperties.SetTextDecorations(deco);
            });
        }
    }
}