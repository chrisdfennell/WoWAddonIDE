using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WoWAddonIDE.Services
{
    public class FindInFilesHit
    {
        public string File { get; set; } = "";
        public int Line { get; set; }
        public int Col { get; set; }
        public string Snippet { get; set; } = "";
    }

    public static class FindInFiles
    {
        public static async Task<List<FindInFilesHit>> SearchAsync(string root, string pattern, bool regex, bool caseSensitive, string[]? filters = null)
        {
            return await Task.Run(() =>
            {
                var hits = new List<FindInFilesHit>();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) || string.IsNullOrEmpty(pattern))
                    return hits;

                var allFiles = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);

                string[] files = allFiles;
                if (filters is { Length: > 0 })
                {
                    var set = new List<string>();
                    foreach (var f in allFiles)
                    {
                        var ext = Path.GetExtension(f);
                        foreach (var flt in filters)
                            if (string.Equals(flt.Trim(), ext, StringComparison.OrdinalIgnoreCase))
                            {
                                set.Add(f);
                                break;
                            }
                    }
                    files = set.ToArray();
                }

                Regex? re = null;
                if (regex)
                {
                    var opts = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    re = new Regex(pattern, opts | RegexOptions.Compiled);
                }

                foreach (var file in files)
                {
                    string text;
                    try { text = File.ReadAllText(file); } catch { continue; }

                    var lines = text.Split('\n');
                    if (regex)
                    {
                        foreach (Match m in re!.Matches(text))
                        {
                            var (ln, col) = PosToLineCol(text, m.Index);
                            hits.Add(new FindInFilesHit { File = file, Line = ln, Col = col, Snippet = GetLine(lines, ln) });
                        }
                    }
                    else
                    {
                        var cmp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                        int idx = 0;
                        while ((idx = text.IndexOf(pattern, idx, cmp)) >= 0)
                        {
                            var (ln, col) = PosToLineCol(text, idx);
                            hits.Add(new FindInFilesHit { File = file, Line = ln, Col = col, Snippet = GetLine(lines, ln) });
                            idx += Math.Max(1, pattern.Length);
                        }
                    }
                }
                return hits;
            });
        }

        private static (int line, int col) PosToLineCol(string text, int pos)
        {
            int line = 1, lastNew = -1;
            for (int i = 0; i < pos; i++)
                if (text[i] == '\n') { line++; lastNew = i; }
            int col = pos - lastNew;
            return (line, col);
        }

        private static string GetLine(string[] lines, int line)
        {
            if (line - 1 >= 0 && line - 1 < lines.Length)
                return lines[line - 1].TrimEnd('\r');
            return "";
        }
    }
}