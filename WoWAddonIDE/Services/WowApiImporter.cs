// Services/WowApiImporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoonSharp.Interpreter;                    // <— NuGet: MoonSharp.Interpreter
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Services
{
    /// <summary>
    /// Imports WoW API docs by executing Blizzard_APIDocumentation*.lua files
    /// in a sandbox and intercepting APIDocumentation:AddDocumentationTable(..).
    /// </summary>
    public static class WowApiImporter
    {
        public static async Task<List<WoWApiEntry>> ImportFromWowAsync(string addOnsPath)
        {
            if (string.IsNullOrWhiteSpace(addOnsPath) || !Directory.Exists(addOnsPath))
                throw new DirectoryNotFoundException("AddOns path not found: " + addOnsPath);

            var docDirs = new[]
            {
                Path.Combine(addOnsPath, "Blizzard_APIDocumentation"),
                Path.Combine(addOnsPath, "Blizzard_APIDocumentationGenerated")
            }.Where(Directory.Exists).ToList();

            if (docDirs.Count == 0)
                throw new DirectoryNotFoundException(
                    "Could not find Blizzard_APIDocumentation* under: " + addOnsPath);

            var files = new List<string>();
            foreach (var dd in docDirs)
                files.AddRange(Directory.EnumerateFiles(dd, "*.lua", SearchOption.AllDirectories));

            return await Task.Run(() => LoadViaMoonSharp(files));
        }

        private static List<WoWApiEntry> LoadViaMoonSharp(List<string> files)
        {
            // Sandbox with basic libs (string/table/math), no IO.
            var script = new Script(CoreModules.Preset_SoftSandbox);

            // Collector where doc files will push their tables
            var collected = new List<Table>();

            // APIDocumentation:AddDocumentationTable stub
            var apiDoc = new Table(script);
            apiDoc.Set("AddDocumentationTable", DynValue.NewCallback((c, a) =>
            {
                if (a.Count > 0 && a[0].Type == DataType.Table)
                    collected.Add(a[0].Table);
                return DynValue.Nil;
            }));
            script.Globals["APIDocumentation"] = apiDoc;

            // Minimal globals some doc files expect
            script.Globals["Enum"] = new Table(script);
            script.Globals["bit"] = new Table(script);
            script.Globals["CreateFromMixins"] = DynValue.NewCallback((c, a) =>
            {
                for (int i = 0; i < a.Count; i++)
                    if (a[i].Type == DataType.Table) return a[i];
                return DynValue.NewTable(script);
            });

            foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var code = File.ReadAllText(file, Encoding.UTF8);
                    script.DoString(code, codeFriendlyName: file);
                }
                catch
                {
                    // ignore individual file failures so we keep going
                }
            }

            var entries = new List<WoWApiEntry>(4096);
            foreach (var tbl in collected)
                ExtractEntriesFromDocTable(tbl, entries);

            // De-dupe by name, keep the richest entry
            return entries
                .GroupBy(e => e.name, StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var best = g.OrderByDescending(x => (x.signature?.Length ?? 0) + (x.description?.Length ?? 0)).First();
                    return new WoWApiEntry { name = best.name, signature = best.signature, description = best.description };
                })
                .OrderBy(e => e.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ExtractEntriesFromDocTable(Table docTable, List<WoWApiEntry> sink)
        {
            var systemName = docTable.Get("Name").AsStringOrNull() ?? "";

            // Functions
            var functions = docTable.Get("Functions");
            if (functions.Type == DataType.Table)
            {
                foreach (var f in functions.Table.Values)
                {
                    if (f.Type != DataType.Table) continue;
                    var fn = f.Table;
                    var fname = fn.Get("Name").AsStringOrNull();
                    if (string.IsNullOrEmpty(fname)) continue;

                    var full = string.IsNullOrEmpty(systemName) ? fname : $"{systemName}.{fname}";
                    sink.Add(new WoWApiEntry
                    {
                        name = full,
                        signature = BuildSignature(full, fn),
                        description = (fn.Get("Documentation").AsStringOrNull() ?? "").Trim()
                    });
                }
            }

            // Structures with methods (Tables -> Functions)
            var tables = docTable.Get("Tables");
            if (tables.Type == DataType.Table)
            {
                foreach (var t in tables.Table.Values)
                {
                    if (t.Type != DataType.Table) continue;
                    var tt = t.Table;
                    var structName = tt.Get("Name").AsStringOrNull() ?? "";

                    var methods = tt.Get("Functions");
                    if (methods.Type != DataType.Table) continue;

                    foreach (var m in methods.Table.Values)
                    {
                        if (m.Type != DataType.Table) continue;
                        var mt = m.Table;
                        var mname = mt.Get("Name").AsStringOrNull();
                        if (string.IsNullOrEmpty(mname)) continue;

                        var full = string.IsNullOrEmpty(structName) ? mname : $"{structName}:{mname}";
                        sink.Add(new WoWApiEntry
                        {
                            name = full,
                            signature = BuildSignature(full, mt),
                            description = (mt.Get("Documentation").AsStringOrNull() ?? "").Trim()
                        });
                    }
                }
            }
        }

        private static string BuildSignature(string fullName, Table fn)
        {
            static string ArgsToString(Table? arr)
            {
                if (arr == null) return "";
                var parts = new List<string>();
                foreach (var v in arr.Values)
                {
                    if (v.Type != DataType.Table) continue;
                    var p = v.Table;
                    var pName = p.Get("Name").AsStringOrNull() ?? "_";
                    var pType = p.Get("Type").AsStringOrNull() ?? "any";
                    var nilable = p.Get("Nilable").AsBoolOrFalse() ? "?" : "";
                    parts.Add($"{pName}: {pType}{nilable}");
                }
                return string.Join(", ", parts);
            }

            var args = fn.Get("Arguments");
            var returns = fn.Get("Returns");

            var argStr = (args.Type == DataType.Table) ? ArgsToString(args.Table) : "";
            var retStr = "";
            if (returns.Type == DataType.Table)
            {
                var parts = new List<string>();
                foreach (var v in returns.Table.Values)
                {
                    if (v.Type != DataType.Table) continue;
                    var r = v.Table;
                    var rType = r.Get("Type").AsStringOrNull() ?? "any";
                    var rName = r.Get("Name").AsStringOrNull();
                    var nilable = r.Get("Nilable").AsBoolOrFalse() ? "?" : "";
                    parts.Add(string.IsNullOrEmpty(rName) ? $"{rType}{nilable}" : $"{rName}: {rType}{nilable}");
                }
                if (parts.Count > 0) retStr = " -> " + string.Join(", ", parts);
            }

            return $"{fullName}({argStr}){retStr}";
        }

        // DynValue helpers
        private static string? AsStringOrNull(this DynValue v) =>
            v.Type == DataType.String ? v.String : null;
        private static bool AsBoolOrFalse(this DynValue v) =>
            v.Type == DataType.Boolean && v.Boolean;
    }
}