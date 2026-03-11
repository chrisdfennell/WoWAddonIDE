// Services/LuaScopeAnalyzer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WoWAddonIDE.Services
{
    /// <summary>
    /// Lightweight scope-aware Lua analyzer for detecting:
    /// - Unused local variables
    /// - Undefined globals
    /// - Wrong WoW API argument counts
    /// </summary>
    public static class LuaScopeAnalyzer
    {
        // Regex patterns
        private static readonly Regex LocalDeclRegex = new(
            @"\blocal\s+(?:function\s+)?(\w+)",
            RegexOptions.Compiled);

        private static readonly Regex FunctionParamsRegex = new(
            @"\bfunction\s*(?:\w+[.:])?\w*\s*\(([^)]*)\)",
            RegexOptions.Compiled);

        private static readonly Regex ForLoopVarsRegex = new(
            @"\bfor\s+([\w,\s]+)\s+in\b",
            RegexOptions.Compiled);

        private static readonly Regex ForNumericRegex = new(
            @"\bfor\s+(\w+)\s*=",
            RegexOptions.Compiled);

        private static readonly Regex FunctionCallRegex = new(
            @"\b([\w.:]+)\s*\(([^)]*)\)",
            RegexOptions.Compiled);

        private static readonly Regex IdentifierRegex = new(
            @"\b([A-Za-z_]\w*)\b",
            RegexOptions.Compiled);

        // Lua built-in globals that should never be flagged
        private static readonly HashSet<string> LuaBuiltins = new(StringComparer.OrdinalIgnoreCase)
        {
            // Lua standard
            "print", "pairs", "ipairs", "type", "tostring", "tonumber", "pcall", "xpcall",
            "error", "assert", "select", "unpack", "rawget", "rawset", "rawequal", "rawlen",
            "setmetatable", "getmetatable", "next", "require", "dofile", "loadstring", "load",
            "collectgarbage", "coroutine", "debug", "io", "math", "os", "package",
            "string", "table", "bit", "bit32",
            // Lua keywords/constants
            "true", "false", "nil", "self", "_G", "_VERSION", "arg",
            // Common WoW globals
            "CreateFrame", "UIParent", "format", "strsplit", "strsub", "strlen",
            "strfind", "strmatch", "strtrim", "strupper", "strlower", "strrep", "strrev",
            "tinsert", "tremove", "wipe", "tContains", "CopyTable",
            "C_Timer", "C_Map", "C_QuestLog", "C_Container", "C_MountJournal",
            "C_Calendar", "C_ChallengeMode", "C_MythicPlus", "C_UnitAuras",
            "C_EncounterJournal", "C_UIWidgetManager", "C_TooltipInfo",
            "C_VoiceChat", "C_GossipInfo", "C_TradeSkillUI", "C_ProfSpecs",
            "hooksecurefunc", "IsAddOnLoaded", "GetAddOnMetadata",
            "SlashCmdList", "LibStub", "InCombatLockdown",
            "UnitName", "UnitHealth", "UnitHealthMax", "UnitLevel", "UnitClass",
            "UnitExists", "UnitIsDead", "UnitGUID", "UnitRace", "UnitFactionGroup",
            "UnitAffectingCombat", "UnitPower", "UnitPowerMax",
            "GetSpellInfo", "GetSpellCooldown", "IsUsableSpell", "IsSpellKnown",
            "GetItemInfo", "GetItemCount", "GetInventoryItemLink",
            "SendChatMessage", "CombatLogGetCurrentEventInfo",
            "GetNumGroupMembers", "IsInGroup", "IsInRaid",
            "GameTooltip", "DEFAULT_CHAT_FRAME", "InterfaceOptionsFrame",
            "StaticPopupDialogs", "StaticPopup_Show",
            "GetCursorPosition", "GetTime", "GetServerTime", "ReloadUI",
            "RunMacroText", "GetBindingKey", "SetBinding", "SaveBindings",
            "EJ_GetInstanceInfo", "EJ_GetEncounterInfo",
            // Vararg
            "...",
        };

        /// <summary>
        /// Run scope analysis on a single Lua file's source code.
        /// </summary>
        public static List<LuaLint.LintDiagnostic> Analyze(
            string file,
            string code,
            HashSet<string>? additionalKnownGlobals = null,
            IReadOnlyList<WoWApiEntry>? apiEntries = null,
            bool checkUnused = true,
            bool checkUndefined = true,
            bool checkArgCount = true)
        {
            var diags = new List<LuaLint.LintDiagnostic>();
            if (string.IsNullOrEmpty(code)) return diags;

            var lines = code.Split('\n');
            var knownGlobals = new HashSet<string>(LuaBuiltins, StringComparer.OrdinalIgnoreCase);
            if (additionalKnownGlobals != null)
                knownGlobals.UnionWith(additionalKnownGlobals);
            if (apiEntries != null)
                knownGlobals.UnionWith(apiEntries.Select(a => a.name.Split('.')[0].Split(':')[0]));

            // Build API signature lookup for arg count checking
            var apiArgCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (apiEntries != null && checkArgCount)
            {
                foreach (var api in apiEntries)
                {
                    var count = CountArgs(api.signature);
                    if (count >= 0) // -1 means varargs, skip
                        apiArgCounts[api.name] = count;
                }
            }

            // Track local variable declarations and their usage
            var localDecls = new List<(string Name, int Line, bool Used)>();
            var allIdentifierUses = new HashSet<(string Name, int Line)>();
            bool inBlockComment = false;

            // Pass 1: collect declarations and all identifier uses
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

                // Strip single-line comments
                var commentIdx = line.IndexOf("--", StringComparison.Ordinal);
                var activeLine = commentIdx >= 0 ? line.Substring(0, commentIdx) : line;

                // Collect local declarations
                if (checkUnused)
                {
                    foreach (Match m in LocalDeclRegex.Matches(activeLine))
                    {
                        var name = m.Groups[1].Value;
                        if (name != "_" && name != "self") // _ is intentionally unused
                            localDecls.Add((name, i + 1, false));
                    }

                    // for-loop variables
                    var forIn = ForLoopVarsRegex.Match(activeLine);
                    if (forIn.Success)
                    {
                        foreach (var v in forIn.Groups[1].Value.Split(','))
                        {
                            var trimmed = v.Trim();
                            if (trimmed.Length > 0 && trimmed != "_")
                                localDecls.Add((trimmed, i + 1, false));
                        }
                    }

                    var forNum = ForNumericRegex.Match(activeLine);
                    if (forNum.Success)
                    {
                        var name = forNum.Groups[1].Value;
                        if (name != "_")
                            localDecls.Add((name, i + 1, false));
                    }

                    // function parameters
                    foreach (Match fm in FunctionParamsRegex.Matches(activeLine))
                    {
                        foreach (var p in fm.Groups[1].Value.Split(','))
                        {
                            var trimmed = p.Trim();
                            if (trimmed.Length > 0 && trimmed != "..." && trimmed != "_" && trimmed != "self")
                                localDecls.Add((trimmed, i + 1, false));
                        }
                    }
                }

                // Collect all identifier uses
                foreach (Match m in IdentifierRegex.Matches(activeLine))
                    allIdentifierUses.Add((m.Groups[1].Value, i + 1));

                // Check argument counts on function calls
                if (checkArgCount && apiArgCounts.Count > 0)
                {
                    foreach (Match m in FunctionCallRegex.Matches(activeLine))
                    {
                        var funcName = m.Groups[1].Value;
                        var argsStr = m.Groups[2].Value.Trim();

                        if (apiArgCounts.TryGetValue(funcName, out var expected))
                        {
                            var actual = CountCallArgs(argsStr);
                            if (actual > expected && expected > 0)
                            {
                                diags.Add(new LuaLint.LintDiagnostic
                                {
                                    File = file,
                                    Line = i + 1,
                                    Severity = "warning",
                                    Message = $"'{funcName}' expects {expected} arg(s) but {actual} provided"
                                });
                            }
                        }
                    }
                }
            }

            // Pass 2: check for unused locals
            if (checkUnused)
            {
                // Mark used locals - a local is "used" if its name appears on any line after its declaration
                for (int d = 0; d < localDecls.Count; d++)
                {
                    var decl = localDecls[d];
                    bool used = allIdentifierUses.Any(u =>
                        u.Name == decl.Name && u.Line > decl.Line);

                    if (!used)
                    {
                        // Also check same-line usage beyond the declaration itself
                        // (e.g., "local x = x + 1" — the x on RHS is the outer x, not unused)
                        // We err on the side of not flagging ambiguous cases
                        var sameLine = allIdentifierUses.Count(u =>
                            u.Name == decl.Name && u.Line == decl.Line);
                        if (sameLine > 1) continue; // used on same line, skip

                        diags.Add(new LuaLint.LintDiagnostic
                        {
                            File = file,
                            Line = decl.Line,
                            Severity = "info",
                            Message = $"Unused local variable '{decl.Name}'"
                        });
                    }
                }
            }

            // Pass 3: check for undefined globals
            if (checkUndefined)
            {
                var localNames = new HashSet<string>(
                    localDecls.Select(d => d.Name),
                    StringComparer.OrdinalIgnoreCase);

                // Reset block comment tracking
                inBlockComment = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].TrimEnd('\r');

                    if (inBlockComment)
                    {
                        if (line.Contains("]]")) inBlockComment = false;
                        continue;
                    }
                    if (line.Contains("--[[")) { inBlockComment = true; continue; }

                    var commentIdx = line.IndexOf("--", StringComparison.Ordinal);
                    var activeLine = commentIdx >= 0 ? line.Substring(0, commentIdx) : line;
                    var trimmed = activeLine.TrimStart();

                    // Skip lines that are local declarations, function defs, or assignments to known patterns
                    if (trimmed.StartsWith("local ") || trimmed.StartsWith("function ") ||
                        trimmed.StartsWith("for ") || trimmed.StartsWith("if ") ||
                        trimmed.StartsWith("elseif ") || trimmed.StartsWith("return ") ||
                        trimmed.StartsWith("end") || trimmed.StartsWith("else") ||
                        trimmed.StartsWith("do") || trimmed.StartsWith("while ") ||
                        trimmed.StartsWith("repeat") || trimmed.StartsWith("until "))
                        continue;

                    // Check for global assignment pattern: IDENTIFIER = ...
                    var globalAssign = Regex.Match(activeLine, @"^(\s*)([A-Z][A-Za-z_]\w*)\s*=");
                    if (globalAssign.Success)
                    {
                        var name = globalAssign.Groups[2].Value;
                        if (!knownGlobals.Contains(name) && !localNames.Contains(name))
                        {
                            // This is an implicit global assignment — flag it
                            diags.Add(new LuaLint.LintDiagnostic
                            {
                                File = file,
                                Line = i + 1,
                                Severity = "warning",
                                Message = $"Implicit global '{name}' (missing 'local'?)"
                            });
                        }
                    }
                }
            }

            return diags;
        }

        /// <summary>Count expected arguments from a signature string like "func(a, b[, c])".</summary>
        private static int CountArgs(string signature)
        {
            var m = Regex.Match(signature, @"\(([^)]*)\)");
            if (!m.Success) return -1;

            var inner = m.Groups[1].Value.Trim();
            if (inner.Length == 0) return 0;
            if (inner.Contains("...")) return -1; // varargs

            // Count required args (not in brackets)
            int count = 0;
            int depth = 0;

            foreach (var ch in inner)
            {
                if (ch == '[') { depth++; }
                else if (ch == ']') { depth--; }
                else if (ch == ',' && depth == 0) { count++; }
            }

            return count + 1; // +1 for last arg (no trailing comma)
        }

        /// <summary>Count actual arguments in a function call.</summary>
        private static int CountCallArgs(string argsStr)
        {
            if (string.IsNullOrWhiteSpace(argsStr)) return 0;

            int count = 1;
            int depth = 0;
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < argsStr.Length; i++)
            {
                var ch = argsStr[i];

                if (inString)
                {
                    if (ch == '\\' && i + 1 < argsStr.Length) { i++; continue; }
                    if (ch == stringChar) inString = false;
                    continue;
                }

                if (ch == '"' || ch == '\'') { inString = true; stringChar = ch; }
                else if (ch == '(' || ch == '{' || ch == '[') depth++;
                else if (ch == ')' || ch == '}' || ch == ']') depth--;
                else if (ch == ',' && depth == 0) count++;
            }

            return count;
        }
    }
}
