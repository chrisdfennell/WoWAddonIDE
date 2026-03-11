using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Services
{
    public static class LuaLint
    {
        /// <summary>
        /// Structured lint diagnostic with file, line, and severity info.
        /// </summary>
        public class LintDiagnostic
        {
            public string File { get; set; } = "";
            public int Line { get; set; }
            public string Severity { get; set; } = "error"; // error, warning, info
            public string Message { get; set; } = "";

            public override string ToString() =>
                Line > 0
                    ? $"[LINT:{Severity}] {Path.GetFileName(File)}:{Line}: {Message}"
                    : $"[LINT:{Severity}] {Path.GetFileName(File)}: {Message}";
        }

        // Common WoW Lua globals that should not be flagged
        private static readonly HashSet<string> KnownWowGlobals = new(StringComparer.OrdinalIgnoreCase)
        {
            "CreateFrame", "UIParent", "print", "format", "strsplit", "tinsert", "tremove",
            "wipe", "CopyTable", "C_Timer", "hooksecurefunc", "IsAddOnLoaded",
            "GetAddOnMetadata", "SlashCmdList", "SLASH_", "LibStub",
            "UnitName", "UnitHealth", "UnitHealthMax", "UnitLevel", "UnitClass",
            "GetSpellInfo", "GetItemInfo", "GetCursorPosition",
            "GameTooltip", "DEFAULT_CHAT_FRAME", "InterfaceOptionsFrame",
            "StaticPopupDialogs", "StaticPopup_Show",
        };

        // Patterns for common mistakes
        private static readonly Regex DeprecatedApi = new(
            @"\b(GetSpellInfo|GetItemInfo|CombatLogGetCurrentEventInfo)\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex GlobalAssign = new(
            @"^(?!.*\blocal\b)([A-Z][A-Za-z_]\w*)\s*=\s*",
            RegexOptions.Compiled);

        private static readonly Regex EmptyBlock = new(
            @"\b(if|for|while|function)\b.*\bthen\b\s*\bend\b|\b(if|for|while|function)\b.*\bdo\b\s*\bend\b",
            RegexOptions.Compiled);

        /// <summary>
        /// Run lint pass and return human-readable messages (backward compatible).
        /// </summary>
        public static List<string> Pass(AddonProject project)
        {
            var diagnostics = Analyze(project);
            var list = new List<string>();
            foreach (var d in diagnostics)
                list.Add(d.ToString());
            if (list.Count == 0)
                list.Add("[LINT] No issues found.");
            return list;
        }

        /// <summary>
        /// Run lint pass and return structured diagnostics.
        /// </summary>
        public static List<LintDiagnostic> Analyze(AddonProject project)
        {
            var diags = new List<LintDiagnostic>();
            try
            {
                foreach (var f in project.Files)
                {
                    if (!f.EndsWith(Constants.LuaExtension, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var code = File.ReadAllText(f);

                        // 1) MoonSharp syntax check
                        SyntaxCheckMoonSharp(f, code, diags);

                        // 2) Static analysis checks
                        StaticAnalysis(f, code, diags);
                    }
                    catch (Exception ex)
                    {
                        diags.Add(new LintDiagnostic
                        {
                            File = f,
                            Severity = "error",
                            Message = ex.Message
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                diags.Add(new LintDiagnostic
                {
                    File = "",
                    Severity = "error",
                    Message = $"Lint failed: {ex.Message}"
                });
            }
            return diags;
        }

        private static void SyntaxCheckMoonSharp(string file, string code, List<LintDiagnostic> diags)
        {
            try
            {
                var script = new Script(CoreModules.None);
                script.LoadString(code);
            }
            catch (SyntaxErrorException ex)
            {
                diags.Add(new LintDiagnostic
                {
                    File = file,
                    Line = ExtractLine(ex.DecoratedMessage),
                    Severity = "error",
                    Message = ex.DecoratedMessage ?? ex.Message
                });
            }
        }

        private static void StaticAnalysis(string file, string code, List<LintDiagnostic> diags)
        {
            StaticAnalysis(file, code, diags, null);
        }

        private static void StaticAnalysis(string file, string code, List<LintDiagnostic> diags,
            IReadOnlyList<WoWApiEntry>? apiEntries)
        {
            var lines = code.Split('\n');
            bool inBlockComment = false;
            int braceBalance = 0;
            int parenBalance = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');

                // Track block comments
                if (inBlockComment)
                {
                    if (line.Contains("]]")) inBlockComment = false;
                    continue;
                }
                if (line.Contains("--[["))
                {
                    inBlockComment = true;
                    continue;
                }

                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("--")) continue; // skip single-line comments

                // Track balance
                foreach (var ch in line)
                {
                    switch (ch)
                    {
                        case '(': parenBalance++; break;
                        case ')': parenBalance--; break;
                        case '{': braceBalance++; break;
                        case '}': braceBalance--; break;
                    }
                }

                // Check for deprecated APIs (WoW 11.0+)
                var dm = DeprecatedApi.Match(line);
                if (dm.Success)
                {
                    diags.Add(new LintDiagnostic
                    {
                        File = file,
                        Line = i + 1,
                        Severity = "warning",
                        Message = $"'{dm.Groups[1].Value}' may be deprecated in recent WoW versions"
                    });
                }

                // Trailing whitespace
                if (line.Length > 0 && (line[^1] == ' ' || line[^1] == '\t'))
                {
                    diags.Add(new LintDiagnostic
                    {
                        File = file,
                        Line = i + 1,
                        Severity = "info",
                        Message = "Trailing whitespace"
                    });
                }
            }

            // Unbalanced delimiters
            if (parenBalance != 0)
            {
                diags.Add(new LintDiagnostic
                {
                    File = file,
                    Severity = "error",
                    Message = $"Unbalanced parentheses (balance: {parenBalance:+#;-#;0})"
                });
            }

            if (braceBalance != 0)
            {
                diags.Add(new LintDiagnostic
                {
                    File = file,
                    Severity = "error",
                    Message = $"Unbalanced braces (balance: {braceBalance:+#;-#;0})"
                });
            }

            // Scope analysis: unused locals, undefined globals, arg count checks
            try
            {
                var scopeDiags = LuaScopeAnalyzer.Analyze(
                    file, code,
                    KnownWowGlobals,
                    apiEntries);
                diags.AddRange(scopeDiags);
            }
            catch
            {
                // scope analysis is best-effort, don't crash lint
            }
        }

        private static int ExtractLine(string? decoratedMessage)
        {
            if (string.IsNullOrWhiteSpace(decoratedMessage)) return 0;
            // MoonSharp format: "(line:col-col): message" or "chunk_N:(line,...): message"
            var m = Regex.Match(decoratedMessage, @"\((\d+)");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var line))
                return line;
            return 0;
        }
    }
}
