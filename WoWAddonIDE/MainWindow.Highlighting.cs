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
                // VS Code Dark+ palette — muted, high-contrast, easy on the eyes
                Set("Comment",      0x6A, 0x99, 0x55);  // olive green
                Set("String",       0xCE, 0x91, 0x78);  // warm salmon
                Set("Number",       0xB5, 0xCE, 0xA8);  // soft green
                Set("Keyword",      0x56, 0x9C, 0xD6);  // steel blue
                Set("Preprocessor", 0xC5, 0x86, 0xC0);  // mauve
                Set("Type",         0x4E, 0xC9, 0xB0);  // teal
                Set("Identifier",   0x9C, 0xDC, 0xFE);  // light cyan
                Set("Operator",     0xD4, 0xD4, 0xD4);  // light gray
                Set("Punctuation",  0xD4, 0xD4, 0xD4);  // light gray
                Set("Method",       0xDC, 0xDC, 0xAA);  // soft gold
                Set("Property",     0x9C, 0xDC, 0xFE);  // light cyan
                Set("Namespace",    0x4E, 0xC9, 0xB0);  // teal
                Set("TableKey",     0x9C, 0xDC, 0xFE);  // light cyan
                Set("Boolean",      0x56, 0x9C, 0xD6);  // steel blue (like keywords)
                Set("Nil",          0x56, 0x9C, 0xD6);  // steel blue (like keywords)
            }
            else
            {
                // VS Code Light+ palette — rich colors on white, easy to read
                Set("Comment",      0x00, 0x80, 0x00);  // green
                Set("String",       0xA3, 0x15, 0x15);  // dark red
                Set("Number",       0x09, 0x86, 0x58);  // teal
                Set("Keyword",      0x00, 0x00, 0xFF);  // blue
                Set("Preprocessor", 0xAF, 0x00, 0xDB);  // purple
                Set("Type",         0x26, 0x7F, 0x99);  // dark cyan
                Set("Identifier",   0x00, 0x10, 0x80);  // navy
                Set("Operator",     0x38, 0x3A, 0x42);  // near-black
                Set("Punctuation",  0x38, 0x3A, 0x42);  // near-black
                Set("Method",       0x79, 0x5E, 0x26);  // brown
                Set("Property",     0x00, 0x10, 0x80);  // navy
                Set("Namespace",    0x26, 0x7F, 0x99);  // dark cyan
                Set("TableKey",     0x00, 0x10, 0x80);  // navy
                Set("Boolean",      0x00, 0x00, 0xFF);  // blue (like keywords)
                Set("Nil",          0x00, 0x00, 0xFF);  // blue (like keywords)
            }
        }
    }
}
