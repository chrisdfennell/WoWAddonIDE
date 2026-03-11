// Services/ReplEnvironment.cs
using System;
using System.Collections.Generic;
using System.Text;
using MoonSharp.Interpreter;

namespace WoWAddonIDE.Services
{
    /// <summary>
    /// Encapsulates a MoonSharp Lua sandbox with WoW API stubs for the REPL.
    /// </summary>
    public sealed class ReplEnvironment
    {
        private Script _script;
        private readonly StringBuilder _printBuffer = new();

        /// <summary>Called when Lua's print() is invoked. Delivers the formatted line.</summary>
        public Action<string>? OutputSink { get; set; }

        public ReplEnvironment()
        {
            _script = CreateSandbox();
        }

        /// <summary>Reset the sandbox (clears all user state).</summary>
        public void Reset()
        {
            _script = CreateSandbox();
        }

        /// <summary>
        /// Execute a line of Lua. Tries as expression first ("return input"),
        /// then as a statement if that fails with a syntax error.
        /// Returns (success, resultText).
        /// </summary>
        public (bool Success, string Result) Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return (true, "");

            _printBuffer.Clear();

            // Try as expression first (prefix with "return")
            try
            {
                var result = _script.DoString("return " + input);
                var output = FormatResult(result);
                return (true, CombineOutput(output));
            }
            catch (SyntaxErrorException)
            {
                // Not an expression, try as statement
            }
            catch (ScriptRuntimeException ex)
            {
                return (false, CombineOutput($"[Runtime Error] {ex.DecoratedMessage ?? ex.Message}"));
            }

            // Try as statement
            try
            {
                _printBuffer.Clear();
                var result = _script.DoString(input);
                var output = FormatResult(result);
                return (true, CombineOutput(output));
            }
            catch (SyntaxErrorException ex)
            {
                return (false, CombineOutput($"[Syntax Error] {ex.DecoratedMessage ?? ex.Message}"));
            }
            catch (ScriptRuntimeException ex)
            {
                return (false, CombineOutput($"[Runtime Error] {ex.DecoratedMessage ?? ex.Message}"));
            }
            catch (Exception ex)
            {
                return (false, CombineOutput($"[Error] {ex.Message}"));
            }
        }

        /// <summary>Load and execute a Lua file in the REPL environment.</summary>
        public (bool Success, string Result) LoadFile(string path)
        {
            try
            {
                _printBuffer.Clear();
                var code = System.IO.File.ReadAllText(path);
                _script.DoString(code);
                return (true, CombineOutput($"-- Loaded: {System.IO.Path.GetFileName(path)}"));
            }
            catch (Exception ex)
            {
                return (false, $"[Error loading file] {ex.Message}");
            }
        }

        private string CombineOutput(string result)
        {
            var printed = _printBuffer.ToString();
            if (string.IsNullOrEmpty(printed))
                return result;
            if (string.IsNullOrEmpty(result) || result == "nil")
                return printed.TrimEnd('\n');
            return printed.TrimEnd('\n') + "\n" + result;
        }

        private static string FormatResult(DynValue? result)
        {
            if (result == null || result.Type == DataType.Void)
                return "";
            if (result.Type == DataType.Nil)
                return "nil";
            if (result.Type == DataType.String)
                return $"\"{result.String}\"";
            if (result.Type == DataType.Table)
                return FormatTable(result.Table, 0);
            return result.ToPrintString();
        }

        private static string FormatTable(Table table, int depth)
        {
            if (depth > 3) return "{ ... }";

            var sb = new StringBuilder("{ ");
            int count = 0;
            foreach (var pair in table.Pairs)
            {
                if (count > 0) sb.Append(", ");
                if (count >= 20) { sb.Append("..."); break; }

                var key = pair.Key.Type == DataType.String ? pair.Key.String : $"[{pair.Key.ToPrintString()}]";
                var val = pair.Value.Type == DataType.Table
                    ? FormatTable(pair.Value.Table, depth + 1)
                    : pair.Value.ToPrintString();
                sb.Append($"{key} = {val}");
                count++;
            }
            sb.Append(" }");
            return sb.ToString();
        }

        private Script CreateSandbox()
        {
            var script = new Script(CoreModules.Preset_SoftSandbox);

            // Override print to capture output
            script.Globals["print"] = (Action<ScriptExecutionContext, CallbackArguments>)LuaPrint;

            // WoW API stubs — return nil/no-ops so addon code doesn't crash
            RegisterWoWStubs(script);

            return script;
        }

        private void LuaPrint(ScriptExecutionContext ctx, CallbackArguments args)
        {
            var parts = new List<string>();
            for (int i = 0; i < args.Count; i++)
                parts.Add(args[i].ToPrintString());

            var line = string.Join("\t", parts);
            _printBuffer.AppendLine(line);
            OutputSink?.Invoke(line);
        }

        private static void RegisterWoWStubs(Script script)
        {
            // Common WoW globals as no-op functions or empty tables
            var stubs = new Dictionary<string, object?>
            {
                // Global functions that return nil
                ["CreateFrame"] = (Func<DynValue>)(() => DynValue.NewTable(new Table(null!))),
                ["UIParent"] = DynValue.NewTable(new Table(null!)),
                ["format"] = script.Globals.Get("string").Table.Get("format"),
                ["strsplit"] = (Func<string, string, DynValue>)((sep, s) => DynValue.NewString(s)),
                ["tinsert"] = script.Globals.Get("table").Table.Get("insert"),
                ["tremove"] = script.Globals.Get("table").Table.Get("remove"),
                ["wipe"] = (Action<Table>)((t) => { foreach (var k in t.Keys) t.Remove(k); }),
                ["InCombatLockdown"] = (Func<bool>)(() => false),
                ["IsAddOnLoaded"] = (Func<string, bool>)((name) => false),
                ["GetAddOnMetadata"] = (Func<string, string, string>)((addon, field) => ""),
                ["GetTime"] = (Func<double>)(() => 0),
                ["GetServerTime"] = (Func<double>)(() => DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                ["ReloadUI"] = (Action)(() => { }),
                ["UnitName"] = (Func<string, string>)((unit) => "Player"),
                ["UnitHealth"] = (Func<string, int>)((unit) => 100),
                ["UnitHealthMax"] = (Func<string, int>)((unit) => 100),
                ["UnitLevel"] = (Func<string, int>)((unit) => 80),
                ["UnitExists"] = (Func<string, bool>)((unit) => unit == "player"),
                ["UnitClass"] = (Func<string, string>)((unit) => "WARRIOR"),
                ["SendChatMessage"] = (Action<string, string, string, string>)((msg, t, l, c) => { }),
                ["RunMacroText"] = (Action<string>)((text) => { }),
                ["hooksecurefunc"] = (Action)(() => { }),
            };

            foreach (var kv in stubs)
            {
                if (kv.Value is DynValue dv)
                    script.Globals.Set(kv.Key, dv);
                else if (kv.Value is Delegate del)
                    script.Globals[kv.Key] = del;
            }

            // C_ namespace stubs (empty tables with __index returning nil)
            var cNamespaces = new[]
            {
                "C_Timer", "C_Map", "C_QuestLog", "C_Container", "C_MountJournal",
                "C_Calendar", "C_ChallengeMode", "C_MythicPlus", "C_UnitAuras",
                "C_EncounterJournal", "C_UIWidgetManager", "C_TooltipInfo",
                "C_VoiceChat", "C_GossipInfo", "C_TradeSkillUI", "C_ProfSpecs"
            };

            foreach (var ns in cNamespaces)
            {
                var t = new Table(script);
                // Set a metatable with __index that returns a no-op function
                var mt = new Table(script);
                mt["__index"] = (Func<Table, string, DynValue>)((tbl, key) =>
                    DynValue.NewCallback((ctx, args) => DynValue.Nil));
                t.MetaTable = mt;
                script.Globals[ns] = t;
            }

            // Common globals that are tables
            script.Globals["SlashCmdList"] = new Table(script);
            script.Globals["StaticPopupDialogs"] = new Table(script);
            script.Globals["DEFAULT_CHAT_FRAME"] = CreateChatFrameStub(script);
            script.Globals["GameTooltip"] = new Table(script);
            script.Globals["LibStub"] = (Func<string, Table>)((name) => new Table(script));
        }

        private static Table CreateChatFrameStub(Script script)
        {
            var t = new Table(script);
            t["AddMessage"] = (Action<Table, string>)((self, msg) => { });
            return t;
        }
    }
}
