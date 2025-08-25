using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private void EnsureLuaHighlightRegistered()
        {
            var existing = HighlightingManager.Instance.GetDefinition("Lua");
            if (existing != null)
            {
                Log("Lua highlight already registered.");
                return;
            }

            Stream? TryOpen()
            {
                try
                {
                    var s = Application.GetResourceStream(new Uri("Resources/Lua.xshd", UriKind.Relative))?.Stream;
                    Log(s != null ? "Lua.xshd found via relative resource." : "Relative resource NOT found.");
                    if (s != null) return s;
                }
                catch (Exception ex) { Log($"Relative resource error: {ex.Message}"); }

                try
                {
                    var asmName = Assembly.GetExecutingAssembly().GetName().Name ?? "WoWAddonIDE";
                    var s = Application.GetResourceStream(new Uri($"/{asmName};component/Resources/Lua.xshd", UriKind.Relative))?.Stream;
                    Log(s != null ? "Lua.xshd found via pack URI." : "Pack URI NOT found.");
                    if (s != null) return s;
                }
                catch (Exception ex) { Log($"Pack URI error: {ex.Message}"); }

                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var p1 = Path.Combine(baseDir, "Resources", "Lua.xshd");
                    var p2 = Path.Combine(baseDir, "Lua.xshd");

                    if (File.Exists(p1)) { Log($"Lua.xshd found on disk: {p1}"); return File.OpenRead(p1); }
                    if (File.Exists(p2)) { Log($"Lua.xshd found on disk: {p2}"); return File.OpenRead(p2); }
                    Log("Disk probe NOT found (bin/Resources or bin root).");
                }
                catch (Exception ex) { Log($"Disk probe error: {ex.Message}"); }

                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    var resName = asm.GetManifestResourceNames()
                                     .FirstOrDefault(n => n.EndsWith("Lua.xshd", StringComparison.OrdinalIgnoreCase));
                    if (resName != null)
                    {
                        Log($"Lua.xshd found as manifest resource: {resName}");
                        return asm.GetManifestResourceStream(resName);
                    }
                    Log("No manifest resource named *Lua.xshd* found.");
                }
                catch (Exception ex) { Log($"Manifest probe error: {ex.Message}"); }

                return null;
            }

            try
            {
                using var stream = TryOpen();
                if (stream == null)
                {
                    Log("Lua.xshd still not found after all probes.");
                    return;
                }

                using var reader = new XmlTextReader(stream);
                var def = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting("Lua", new[] { ".lua" }, def);
                Log("Lua highlighting registered successfully.");
            }
            catch (Exception ex)
            {
                Log($"Failed to load/register Lua highlighting: {ex.Message}");
            }
        }

        private void EnsureTocHighlightRegistered()
        {
            if (HighlightingManager.Instance.GetDefinition("WoWTOC") != null)
            {
                Log("TOC highlight already registered.");
                return;
            }

            Stream? TryOpen()
            {
                try
                {
                    foreach (var candidate in new[] { "Resources/WoWTOC.xshd", "Resources/wowtoc.xshd", "Resources/WowToc.xshd" })
                    {
                        var s = Application.GetResourceStream(new Uri(candidate, UriKind.Relative))?.Stream;
                        if (s != null) { Log($"WoWTOC.xshd found via relative resource: {candidate}"); return s; }
                    }
                    Log("Relative resource NOT found for WoWTOC.xshd.");
                }
                catch (Exception ex) { Log($"Relative resource error (TOC): {ex.Message}"); }

                try
                {
                    var asmName = Assembly.GetExecutingAssembly().GetName().Name ?? "WoWAddonIDE";
                    foreach (var candidate in new[] { $"/{asmName};component/Resources/WoWTOC.xshd", $"/{asmName};component/Resources/wowtoc.xshd", $"/{asmName};component/Resources/WowToc.xshd" })
                    {
                        var s = Application.GetResourceStream(new Uri(candidate, UriKind.Relative))?.Stream;
                        if (s != null) { Log($"WoWTOC.xshd found via pack URI: {candidate}"); return s; }
                    }
                    Log("Pack URI NOT found for WoWTOC.xshd.");
                }
                catch (Exception ex) { Log($"Pack URI error (TOC): {ex.Message}"); }

                try
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    foreach (var p in new[] { Path.Combine(baseDir, "Resources", "WoWTOC.xshd"), Path.Combine(baseDir, "Resources", "wowtoc.xshd"), Path.Combine(baseDir, "WoWTOC.xshd"), Path.Combine(baseDir, "wowtoc.xshd") })
                    {
                        if (File.Exists(p)) { Log($"WoWTOC.xshd found on disk: {p}"); return File.OpenRead(p); }
                    }
                    Log("Disk probe NOT found for WoWTOC.xshd.");
                }
                catch (Exception ex) { Log($"Disk probe error (TOC): {ex.Message}"); }

                try
                {
                    var asm = Assembly.GetExecutingAssembly();
                    var resName = asm.GetManifestResourceNames()
                                     .FirstOrDefault(n => n.EndsWith("WoWTOC.xshd", StringComparison.OrdinalIgnoreCase));
                    if (resName != null)
                    {
                        Log($"WoWTOC.xshd found as manifest resource: {resName}");
                        return asm.GetManifestResourceStream(resName);
                    }
                    Log("No manifest resource named *WoWTOC.xshd* found.");
                }
                catch (Exception ex) { Log($"Manifest probe error (TOC): {ex.Message}"); }

                return null;
            }

            try
            {
                using var stream = TryOpen();
                if (stream == null)
                {
                    Log("WoWTOC.xshd still not found after all probes.");
                    return;
                }

                using var reader = new XmlTextReader(stream);
                var def = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);
                HighlightingManager.Instance.RegisterHighlighting("WoWTOC", new[] { ".toc" }, def);
                Log("TOC highlighting registered.");
            }
            catch (Exception ex)
            {
                Log($"Failed to register TOC highlighting: {ex.Message}");
            }
        }

        private void RetintHighlighting(IHighlightingDefinition def, bool dark)
        {
            if (def == null) return;

            var named = def.NamedHighlightingColors;

            HighlightingColor? Find(string name) =>
                named.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

            void Set(string name, byte r, byte g, byte b)
            {
                var hc = Find(name);
                if (hc != null)
                {
                    hc.Foreground = new SimpleHighlightingBrush(
                        System.Windows.Media.Color.FromRgb(r, g, b));
                }
            }

            if (dark)
            {
                Set("Comment", 0x9E, 0xE8, 0x6C);
                Set("String", 0xFF, 0xC7, 0x80);
                Set("Number", 0xB2, 0xFF, 0x59);
                Set("Keyword", 0x8A, 0xB4, 0xFF);
                Set("Preprocessor", 0xFF, 0xAB, 0x91);
                Set("Type", 0x80, 0xCB, 0xC4);
                Set("Identifier", 0xE6, 0xEE, 0xFF);
                Set("Operator", 0xF5, 0x78, 0x5D);
                Set("Punctuation", 0xC7, 0x92, 0xEA);
                Set("Method", 0xFF, 0xEA, 0x00);
                Set("Property", 0xCF, 0xD8, 0xDC);
                Set("Namespace", 0xA7, 0xFF, 0xEB);
                Set("TableKey", 0xFF, 0xD7, 0x00);
                Set("Boolean", 0x64, 0xFF, 0xDA);
                Set("Nil", 0xFF, 0x8A, 0x80);
            }
            else
            {
                Set("Comment", 0x43, 0xA0, 0x47);
                Set("String", 0xD8, 0x63, 0x15);
                Set("Number", 0x0D, 0x47, 0xA1);
                Set("Keyword", 0x6A, 0x1B, 0x9A);
                Set("Preprocessor", 0xC2, 0x21, 0x21);
                Set("Type", 0x00, 0x57, 0x73);
                Set("Identifier", 0x21, 0x21, 0x21);
                Set("Operator", 0x88, 0x17, 0x11);
                Set("Punctuation", 0x4A, 0x14, 0x8C);
                Set("Method", 0xE6, 0x6F, 0x00);
                Set("Property", 0x37, 0x47, 0x4F);
                Set("Namespace", 0x00, 0x77, 0x5A);
                Set("TableKey", 0xB7, 0x77, 0x00);
                Set("Boolean", 0x00, 0x79, 0x6B);
                Set("Nil", 0xB7, 0x1C, 0x1C);
            }
        }
    }
}
