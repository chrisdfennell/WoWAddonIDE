// Services/SavedVariablesReader.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WoWAddonIDE.Services
{
    public sealed class SavedVarNode
    {
        public string Name { get; set; } = "";
        public string? ValueDisplay { get; set; } // null if object
        public List<SavedVarNode> Children { get; } = new();
        public bool IsLeaf => Children.Count == 0;
        public override string ToString() => ValueDisplay is null ? Name : $"{Name}: {ValueDisplay}";
    }

    public static class SavedVariablesReader
    {
        public static Dictionary<string, SavedVarNode> ParseGlobals(string luaText)
        {
            var tok = new Tokenizer(luaText);
            var result = new Dictionary<string, SavedVarNode>(StringComparer.OrdinalIgnoreCase);

            while (tok.SkipTrivia())
            {
                // Expect identifier on LHS (global)
                var name = tok.ReadIdentifierOrString();
                if (string.IsNullOrEmpty(name)) break;
                tok.Require('=');

                var node = ParseValue(tok, name);
                if (node != null)
                    result[name] = node;

                tok.SkipUntil(';', '\n'); // tolerate semicolons/newlines between statements
            }
            return result;
        }

        private static SavedVarNode? ParseValue(Tokenizer t, string nameForNode)
        {
            if (!t.SkipTrivia()) return null;

            var ch = t.Peek();
            if (ch == '{')
            {
                t.Read(); // {
                var obj = new SavedVarNode { Name = nameForNode };
                int autoIndex = 1;

                while (t.SkipTrivia() && t.Peek() != '}')
                {
                    // key = value | value (implicit numeric key)
                    SavedVarNode? child;

                    if (t.Peek() == '[') // [expr] = value
                    {
                        t.Read(); // [
                        var key = ReadBracketKey(t);
                        t.Require(']');
                        t.Require('=');
                        child = ParseValue(t, key);
                        if (child != null) obj.Children.Add(child);
                    }
                    else
                    {
                        // lookahead: identifier = value  OR  bare value
                        var mark = t.Mark();
                        var maybeIdent = t.ReadIdentifierOrString(allowString: true);
                        if (!string.IsNullOrEmpty(maybeIdent) && t.SkipTrivia() && t.TryConsume('='))
                        {
                            child = ParseValue(t, maybeIdent);
                            if (child != null) obj.Children.Add(child);
                        }
                        else
                        {
                            // revert and treat as value-only (array style)
                            t.Reset(mark);
                            var val = ParseScalarOrTable(t, autoIndex.ToString());
                            if (val != null) obj.Children.Add(val);
                            autoIndex++;
                        }
                    }

                    t.SkipTrivia();
                    t.TryConsume(','); // optional comma
                }
                t.Require('}');

                return obj;
            }

            // scalar
            return ParseScalarOrTable(t, nameForNode);
        }

        private static SavedVarNode? ParseScalarOrTable(Tokenizer t, string name)
        {
            if (!t.SkipTrivia()) return null;

            var ch = t.Peek();
            if (ch == '{') return ParseValue(t, name);

            if (ch == '"' || ch == '\'')
            {
                var s = t.ReadString();
                return new SavedVarNode { Name = name, ValueDisplay = s };
            }

            // number / bool / nil / bare identifier
            var ident = t.ReadIdentifierOrNumberOrLiteral();
            if (ident == null) return null;

            return new SavedVarNode { Name = name, ValueDisplay = ident };
        }

        private static string ReadBracketKey(Tokenizer t)
        {
            t.SkipTrivia();
            var ch = t.Peek();
            if (ch == '"' || ch == '\'') return t.ReadString();
            // numeric/identifier expression inside brackets is common; read simply until closing bracket or =
            var sb = new StringBuilder();
            int depth = 0;
            while (!t.EOF)
            {
                var c = t.Read();
                if (c == '[') depth++;
                else if (c == ']' && depth-- <= 0)
                { // we've consumed the final ']' in caller
                    t.Back();
                    break;
                }
                else if (c == '\n' || c == '\r') break;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        // ---------------- Tokenizer (Lua-lite) ----------------
        private sealed class Tokenizer
        {
            private readonly string _s;
            private int _i;

            public Tokenizer(string s) { _s = s ?? ""; }

            public bool EOF => _i >= _s.Length;
            public char Peek() => _i < _s.Length ? _s[_i] : '\0';
            public char Read() => _i < _s.Length ? _s[_i++] : '\0';
            public void Back() { if (_i > 0) _i--; }

            public int Mark() => _i;
            public void Reset(int mark) => _i = mark;

            public bool SkipTrivia()
            {
                bool moved = false;
                while (!EOF)
                {
                    var c = Peek();
                    if (char.IsWhiteSpace(c)) { _i++; moved = true; continue; }
                    if (c == '-' && Peek2() == '-')
                    {
                        // comment: -- ... or --[[ ... ]]
                        _i += 2;
                        if (Peek() == '[' && Peek2() == '[')
                        {
                            _i += 2;
                            while (!EOF)
                            {
                                if (Peek() == ']' && Peek2() == ']') { _i += 2; break; }
                                _i++;
                            }
                        }
                        else
                        {
                            while (!EOF && Peek() != '\n') _i++;
                        }
                        moved = true;
                        continue;
                    }
                    break;
                }
                return !EOF || moved;
            }

            public char Peek2() => _i + 1 < _s.Length ? _s[_i + 1] : '\0';

            public bool TryConsume(char ch)
            {
                if (Peek() == ch) { _i++; return true; }
                return false;
            }

            public void Require(char ch)
            {
                SkipTrivia();
                if (!TryConsume(ch))
                    throw new FormatException($"Expected '{ch}' at {_i}");
            }

            public void SkipUntil(params char[] stop)
            {
                var set = new HashSet<char>(stop);
                while (!EOF && !set.Contains(Peek())) _i++;
            }

            public string? ReadIdentifierOrNumberOrLiteral()
            {
                SkipTrivia();
                if (EOF) return null;

                var ch = Peek();

                if (ch == '"' || ch == '\'') return ReadString();

                // number
                if (char.IsDigit(ch) || (ch == '.' && char.IsDigit(Peek2())))
                {
                    var sb = new StringBuilder();
                    bool dot = false;
                    while (!EOF)
                    {
                        var c = Peek();
                        if (char.IsDigit(c)) { sb.Append(c); _i++; }
                        else if (!dot && c == '.') { dot = true; sb.Append(c); _i++; }
                        else break;
                    }
                    return sb.ToString();
                }

                // identifier / literal
                if (char.IsLetter(ch) || ch == '_')
                {
                    var sb = new StringBuilder();
                    while (!EOF)
                    {
                        var c = Peek();
                        if (char.IsLetterOrDigit(c) || c == '_') { sb.Append(c); _i++; }
                        else break;
                    }
                    var id = sb.ToString();
                    return id switch
                    {
                        "true" => "true",
                        "false" => "false",
                        "nil" => "nil",
                        _ => id
                    };
                }

                return null;
            }

            public string ReadIdentifierOrString(bool allowString = false)
            {
                SkipTrivia();
                if (allowString && (Peek() == '"' || Peek() == '\'')) return ReadString();

                var sb = new StringBuilder();
                if (!(char.IsLetter(Peek()) || Peek() == '_')) return "";
                while (!EOF)
                {
                    var c = Peek();
                    if (char.IsLetterOrDigit(c) || c == '_') { sb.Append(c); _i++; }
                    else break;
                }
                return sb.ToString();
            }

            public string ReadString()
            {
                SkipTrivia();
                var quote = Read(); // ' or "
                var sb = new StringBuilder();
                while (!EOF)
                {
                    var c = Read();
                    if (c == '\\')
                    {
                        if (EOF) break;
                        var e = Read();
                        sb.Append(e switch
                        {
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            '\\' => '\\',
                            '"' => '"',
                            '\'' => '\'',
                            _ => e
                        });
                    }
                    else if (c == quote) break;
                    else sb.Append(c);
                }
                return sb.ToString();
            }
        }

        // ---- Helpers for UI ----
        public static SavedVarNode ToTree(string globalName, object valueOrNullIfObj, List<SavedVarNode>? childrenIfObj)
        {
            return new SavedVarNode
            {
                Name = globalName,
                ValueDisplay = childrenIfObj == null ? Convert.ToString(valueOrNullIfObj) : null
            };
        }
    }
}