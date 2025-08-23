using MoonSharp.Interpreter;
using System;
using System.Collections.Generic;
using System.IO;
using WoWAddonIDE.Models;

namespace WoWAddonIDE.Services
{
    public static class LuaLint
    {
        /// <summary>
        /// Very basic syntax check using MoonSharp parser. Not 100% WoW-Lua but catches many mistakes.
        /// </summary>
        public static List<string> Pass(AddonProject project)
        {
            var list = new List<string>();
            try
            {
                foreach (var f in project.Files)
                {
                    if (!f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var code = File.ReadAllText(f);
                        Script script = new Script(CoreModules.None);
                        script.LoadString(code); // parse only
                    }
                    catch (SyntaxErrorException ex)
                    {
                        list.Add($"[LINT] {Path.GetFileName(f)}: {ex.DecoratedMessage}");
                    }
                    catch (Exception ex)
                    {
                        list.Add($"[LINT] {Path.GetFileName(f)}: {ex.Message}");
                    }
                }
                if (list.Count == 0) list.Add("[LINT] No syntax errors found.");
            }
            catch (Exception ex)
            {
                list.Add($"[LINT] Failed: {ex.Message}");
            }
            return list;
        }
    }
}