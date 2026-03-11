// File: MainWindow.Folding.cs
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<TextEditor, FoldingManager> _foldingManagers = new();
        private DispatcherTimer? _foldingTimer;

        private void Folding_Init()
        {
            _foldingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _foldingTimer.Tick += (s, e) => Folding_UpdateActive();
            _foldingTimer.Start();
        }

        internal void Folding_SetupForEditor(TextEditor editor, string filePath)
        {
            if (!filePath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) return;

            var manager = FoldingManager.Install(editor.TextArea);
            _foldingManagers[editor] = manager;
            Folding_Update(editor, manager);
        }

        internal void Folding_RemoveForEditor(TextEditor editor)
        {
            if (_foldingManagers.TryGetValue(editor, out var manager))
            {
                FoldingManager.Uninstall(manager);
                _foldingManagers.Remove(editor);
            }
        }

        private void Folding_UpdateActive()
        {
            var ed = ActiveEditor();
            if (ed != null && _foldingManagers.TryGetValue(ed, out var manager))
                Folding_Update(ed, manager);
        }

        private static void Folding_Update(TextEditor editor, FoldingManager manager)
        {
            var folds = LuaFoldingStrategy.CreateFoldings(editor.Document);
            manager.UpdateFoldings(folds, -1);
        }
    }

    /// <summary>
    /// Folding strategy for Lua files. Detects function/if/for/while/do/repeat blocks
    /// and multi-line comments.
    /// </summary>
    internal static class LuaFoldingStrategy
    {
        public static IEnumerable<NewFolding> CreateFoldings(TextDocument document)
        {
            var folds = new List<NewFolding>();
            var text = document.Text;

            // Stack of (keyword, startOffset)
            var stack = new Stack<(string keyword, int offset)>();

            for (int i = 1; i <= document.LineCount; i++)
            {
                var line = document.GetLineByNumber(i);
                var rawText = document.GetText(line.Offset, line.Length);
                var lineText = rawText.TrimStart();

                if (lineText.Length == 0) continue;

                // Multi-line comment: --[[ ... ]]
                if (lineText.StartsWith("--[["))
                {
                    int start = line.Offset + rawText.IndexOf("--[[");
                    int endIdx = text.IndexOf("]]", start + 4);
                    if (endIdx >= 0)
                    {
                        folds.Add(new NewFolding(start, endIdx + 2) { Name = "--[[ ... ]]" });
                    }
                    continue;
                }

                // Skip single-line comments
                if (lineText.StartsWith("--")) continue;

                // Strip strings and comments for keyword detection
                var stripped = StripStringsAndComments(lineText);

                // Detect block openers
                if (IsBlockOpener(stripped, out var keyword))
                {
                    stack.Push((keyword, line.Offset));
                }

                // Detect 'end' or 'until'
                if (IsBlockCloser(stripped))
                {
                    if (stack.Count > 0)
                    {
                        var opener = stack.Pop();
                        if (line.EndOffset > opener.offset + 1)
                        {
                            folds.Add(new NewFolding(opener.offset, line.EndOffset)
                            {
                                Name = opener.keyword + " ..."
                            });
                        }
                    }
                }
            }

            folds.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            return folds;
        }

        private static bool IsBlockOpener(string line, out string keyword)
        {
            keyword = "";

            // function ...
            if (ContainsKeyword(line, "function") && line.Contains('('))
            {
                // Single-line function: function() end
                if (line.Contains("end")) return false;
                keyword = "function";
                return true;
            }

            // if ... then
            if (ContainsKeyword(line, "if") && line.Contains("then"))
            {
                // Single-line if: if x then y end
                if (ContainsKeyword(line, "end")) return false;
                // elseif is not a new block
                if (StartsWithKeyword(line.TrimStart(), "elseif")) return false;
                keyword = "if";
                return true;
            }

            // for ... do
            if (StartsWithKeyword(line.TrimStart(), "for") && line.Contains("do"))
            {
                if (line.TrimEnd().EndsWith("end")) return false;
                keyword = "for";
                return true;
            }

            // while ... do
            if (StartsWithKeyword(line.TrimStart(), "while") && line.Contains("do"))
            {
                if (line.TrimEnd().EndsWith("end")) return false;
                keyword = "while";
                return true;
            }

            // do (standalone)
            if (line.Trim() == "do")
            {
                keyword = "do";
                return true;
            }

            // repeat ... until
            if (StartsWithKeyword(line.TrimStart(), "repeat"))
            {
                keyword = "repeat";
                return true;
            }

            return false;
        }

        private static bool IsBlockCloser(string line)
        {
            var t = line.Trim();
            if (t == "end" || t.StartsWith("end ") || t.StartsWith("end)")
                || t.StartsWith("end,") || t.StartsWith("end;")
                || t == "end)" || t == "end," || t == "end;")
                return true;

            return StartsWithKeyword(t, "until");
        }

        private static bool StartsWithKeyword(string line, string keyword)
        {
            if (!line.StartsWith(keyword)) return false;
            if (line.Length == keyword.Length) return true;
            char next = line[keyword.Length];
            return !char.IsLetterOrDigit(next) && next != '_';
        }

        private static bool ContainsKeyword(string line, string keyword)
        {
            int idx = line.IndexOf(keyword);
            while (idx >= 0)
            {
                bool prevOk = idx == 0 || (!char.IsLetterOrDigit(line[idx - 1]) && line[idx - 1] != '_');
                int after = idx + keyword.Length;
                bool nextOk = after >= line.Length || (!char.IsLetterOrDigit(line[after]) && line[after] != '_');
                if (prevOk && nextOk) return true;
                idx = line.IndexOf(keyword, idx + 1);
            }
            return false;
        }

        private static string StripStringsAndComments(string line)
        {
            var sb = new System.Text.StringBuilder(line.Length);
            bool inString = false;
            char stringChar = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inString)
                {
                    if (c == stringChar && (i == 0 || line[i - 1] != '\\'))
                        inString = false;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringChar = c;
                    continue;
                }
                if (c == '-' && i + 1 < line.Length && line[i + 1] == '-')
                    break; // rest is comment
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
