// Services/LuaDiagnosticsTransformer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows; // TextDecoration, TextDecorationCollection, TextDecorationLocation
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Media = System.Windows.Media; // WPF brushes/colors

namespace WoWAddonIDE.Services
{
    public sealed class LuaDiagnosticsTransformer : DocumentColorizingTransformer
    {
        public enum Severity { Info, Warning, Error }

        private sealed class Diag
        {
            public int Start;
            public int Length;
            public Severity Sev;
            public string Message = "";
        }

        private readonly List<Diag> _diags = new();

        /// <summary>Optional API entries for scope analysis (unused vars, undefined globals, arg counts).</summary>
        public IReadOnlyList<WoWApiEntry>? ApiEntries { get; set; }

        public void Reanalyze(string text)
        {
            _diags.Clear();
            if (string.IsNullOrEmpty(text)) return;

            var lines = text.Split('\n');
            int offset = 0;
            int parenBalance = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                var idxTodo = line.IndexOf("TODO", StringComparison.OrdinalIgnoreCase);
                if (idxTodo >= 0)
                    _diags.Add(new Diag { Start = offset + idxTodo, Length = 4, Sev = Severity.Info, Message = "TODO" });

                var idxFix = line.IndexOf("FIXME", StringComparison.OrdinalIgnoreCase);
                if (idxFix >= 0)
                    _diags.Add(new Diag { Start = offset + idxFix, Length = 5, Sev = Severity.Warning, Message = "FIXME" });

                int endTrim = line.Length;
                while (endTrim > 0 && (line[endTrim - 1] == ' ' || line[endTrim - 1] == '\t' || line[endTrim - 1] == '\r'))
                    endTrim--;
                if (endTrim < line.Length && endTrim > 0)
                    _diags.Add(new Diag
                    {
                        Start = offset + endTrim - 1,
                        Length = (line.Length - endTrim + 1),
                        Sev = Severity.Info,
                        Message = "Trailing whitespace"
                    });

                foreach (var ch in line)
                {
                    if (ch == '(') parenBalance++;
                    else if (ch == ')') parenBalance--;
                }

                offset += line.Length + 1; // include '\n'
            }

            if (parenBalance != 0)
            {
                _diags.Add(new Diag
                {
                    Start = Math.Max(0, text.Length - 1),
                    Length = 1,
                    Sev = Severity.Error,
                    Message = "Unbalanced parentheses"
                });
            }

            // Scope analysis: unused locals, undefined globals, wrong arg counts
            try
            {
                var scopeDiags = LuaScopeAnalyzer.Analyze(
                    "", text,
                    additionalKnownGlobals: null,
                    apiEntries: ApiEntries,
                    checkUnused: true,
                    checkUndefined: true,
                    checkArgCount: true);

                foreach (var sd in scopeDiags)
                {
                    if (sd.Line <= 0 || sd.Line > lines.Length) continue;

                    // Find offset for the line
                    int lineOffset = 0;
                    for (int li = 0; li < sd.Line - 1 && li < lines.Length; li++)
                        lineOffset += lines[li].Length + 1;

                    var sev = sd.Severity switch
                    {
                        "error" => Severity.Error,
                        "warning" => Severity.Warning,
                        _ => Severity.Info
                    };

                    _diags.Add(new Diag
                    {
                        Start = lineOffset,
                        Length = Math.Min(lines[sd.Line - 1].TrimEnd('\r').Length, 200),
                        Sev = sev,
                        Message = sd.Message
                    });
                }
            }
            catch
            {
                // scope analysis is best-effort
            }
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (_diags.Count == 0) return;

            int lineStart = line.Offset;
            int lineEnd = lineStart + line.Length;

            foreach (var d in _diags)
            {
                int start = Math.Max(d.Start, lineStart);
                int end = Math.Min(d.Start + d.Length, lineEnd);
                if (start >= end) continue;

                var brush = BrushFor(d.Sev);
                var pen = new Media.Pen(brush, 1) { DashStyle = Media.DashStyles.Dash };

                ChangeLinePart(start, end, element =>
                {
                    var props = element.TextRunProperties;

                    // Color the text
                    props.SetForegroundBrush(brush);

                    // Add a dashed underline
                    var decoration = new TextDecoration
                    {
                        Location = TextDecorationLocation.Underline,
                        Pen = pen,
                        PenThicknessUnit = TextDecorationUnit.FontRecommended
                    };

                    var list = props.TextDecorations?.ToList() ?? new List<TextDecoration>();
                    list.Add(decoration);
                    props.SetTextDecorations(new TextDecorationCollection(list));
                });
            }
        }

        private static Media.Brush BrushFor(Severity sev) => sev switch
        {
            Severity.Error => new Media.SolidColorBrush(Media.Color.FromRgb(0xE0, 0x5A, 0x4A)),
            Severity.Warning => new Media.SolidColorBrush(Media.Color.FromRgb(0xD7, 0x91, 0x00)),
            _ => new Media.SolidColorBrush(Media.Color.FromRgb(0x2A, 0x88, 0xD8)),
        };
    }
}