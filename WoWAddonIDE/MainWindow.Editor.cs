// File: MainWindow.Editor.cs
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.VisualBasic; // for Interaction.InputBox
using Microsoft.Win32;       // for OpenFileDialog
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        // Keep a single Find/Replace window
        private Windows.FindReplaceWindow? _findWindow;

        // Default args for StyLua: read from STDIN, use the real file path for config discovery
        private const string DEFAULT_STYLUA_ARGS = "--search-parent-directories --stdin-filepath \"{file}\" -";

        // Detect whether the arg template intends to read from STDIN.
        // We treat a trailing '-' (… '-') or explicit {stdin} token as "STDIN mode".
        private static bool ArgTemplateNeedsStdIn(string template)
        {
            if (string.IsNullOrWhiteSpace(template)) return false;
            var t = template.Trim();
            if (t.EndsWith(" -") || t.EndsWith("-")) return true;
            return t.IndexOf("{stdin}", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Case-insensitive replace helper (works on older frameworks)
        private static string ReplaceCI(string input, string oldValue, string newValue)
        {
            if (input == null) return "";
            if (string.IsNullOrEmpty(oldValue)) return input;
            return Regex.Replace(
                input,
                Regex.Escape(oldValue),
                (newValue ?? "").Replace("$", "$$"),
                RegexOptions.IgnoreCase
            );
        }


        private TextEditor? ActiveEditor()
        {
            if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed)
                return ed;
            return null;
        }

        private IEnumerable<TextEditor> AllEditors()
        {
            foreach (var obj in EditorTabs.Items)
                if (obj is TabItem t && t.Content is TextEditor ed)
                    yield return ed;
        }

        private void ShowFindWindow(bool focusReplace = false)
        {
            if (_findWindow == null)
            {
                var ed = ActiveEditor();
                var seed = ed?.SelectedText;
                _findWindow = new Windows.FindReplaceWindow(ActiveEditor, AllEditors, seed, focusReplace)
                {
                    Owner = this
                };
                _findWindow.Closed += (_, __) => _findWindow = null;
                _findWindow.Show();
                _findWindow.Activate();
            }
            else
            {
                _findWindow.Activate();
                if (focusReplace)
                {
                    try { _findWindow.FocusReplaceFromHost(); } catch { /* optional helper; ignore if absent */ }
                }
            }
        }

        private void OpenFileInTab(string path)
        {
            // If already open, focus it
            foreach (TabItem tab in EditorTabs.Items)
            {
                if (string.Equals(tab.Tag as string, path, StringComparison.OrdinalIgnoreCase))
                {
                    EditorTabs.SelectedItem = tab;
                    return;
                }
            }

            var editor = new TextEditor
            {
                ShowLineNumbers = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            ThemeManager.ApplyToEditor(editor);
            editor.Options.HighlightCurrentLine = true;
            editor.Options.IndentationSize = 4;
            editor.Options.ConvertTabsToSpaces = true;

            editor.TextArea.TextView.ElementGenerators.Add(new WowColorInlineGenerator());
            WireWowColorClick(editor);

            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                var def = _completion.LuaHighlight ?? HighlightingManager.Instance.GetDefinition("Lua");
                editor.SyntaxHighlighting = def;
                RetintHighlighting(def, IsDarkThemeActive());
            }
            else if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
            }
            else if (path.EndsWith(".toc", StringComparison.OrdinalIgnoreCase))
            {
                var def = HighlightingManager.Instance.GetDefinition("WoWTOC");
                editor.SyntaxHighlighting = def;
                RetintHighlighting(def, IsDarkThemeActive());
            }

            editor.Text = File.ReadAllText(path);

            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                var diag = new LuaDiagnosticsTransformer();
                editor.TextArea.TextView.LineTransformers.Add(diag);
                editor.TextChanged += (s, e) => diag.Reanalyze(editor.Text);
                diag.Reanalyze(editor.Text);
            }

            editor.TextArea.TextEntered += (s, e) =>
            {
                if (e.Text.Length == 1)
                {
                    var ch = e.Text[0];

                    if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '!' || ch == '.')
                    {
                        var word = CompletionService.GetCurrentWord(editor.TextArea);
                        if (word.Length >= 1)
                            _completion.ShowCompletion(editor.TextArea, word);
                    }

                    if (ch == '(')
                    {
                        var fname = CompletionService.GetWordBeforeChar(editor.TextArea, '(');
                        if (!string.IsNullOrWhiteSpace(fname))
                            _completion.ShowParameterHints(editor.TextArea, fname);
                    }
                }
            };

            var tv = editor.TextArea.TextView;
            tv.MouseHover += (s, e) =>
            {
                var pos = tv.GetPositionFloor(e.GetPosition(tv));
                if (!pos.HasValue) { editor.ToolTip = null; return; }

                int offset = editor.Document.GetOffset(pos.Value.Location);
                var word = GetWordAtOffset(editor.Text, offset);
                if (string.IsNullOrWhiteSpace(word)) { editor.ToolTip = null; return; }

                if (_apiDocs.TryGetValue(word, out var entry))
                {
                    var text = $"{entry.name}\n{entry.signature}\n{entry.description}";
                    var tt = new ToolTip { Content = text };
                    ToolTipService.SetShowDuration(tt, 20000);
                    editor.ToolTip = tt;
                }
                else
                {
                    editor.ToolTip = null;
                }
            };
            tv.MouseHoverStopped += (s, e) => { editor.ToolTip = null; };

            editor.TextChanged += (s, e) =>
            {
                MarkTabDirty(path, true);
                if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                    RefreshOutlineForActive();

                RefreshColorSwatches(editor.Text);
            };

            RefreshColorSwatches(editor.Text);

            // Editor-local shortcuts
            editor.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => ToggleComment(editor)),
                new KeyGesture(Key.Oem2, ModifierKeys.Control))); // Ctrl+/

            editor.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => DuplicateLine(editor)),
                new KeyGesture(Key.D, ModifierKeys.Control))); // Ctrl+D

            editor.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => GoToDefinition(editor)),
                new KeyGesture(Key.F12)));

            AttachEditorContextMenu(editor, path);

            // Enable rectangular/column selection (Alt+drag)
            editor.Options.EnableRectangularSelection = true;

            // Auto-close Lua block keywords (function/if/for/while/do → end)
            if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
            {
                editor.TextArea.TextEntering += (s, e) =>
                {
                    if (e.Text == "\n" || e.Text == "\r")
                    {
                        AutoInsertLuaEnd(editor);
                    }
                };
            }

            // Bracket matching highlight
            AttachBracketHighlighting(editor);

            var tabItem = new TabItem
            {
                Header = Path.GetFileName(path),
                Content = editor,
                Tag = path
            };

            EditorTabs.Items.Add(tabItem);
            EditorTabs.SelectedItem = tabItem;

            // Code folding for Lua files
            Folding_SetupForEditor(editor, path);

            // Minimap scroll sync
            Minimap_HookEditorScroll(editor);

            if (path.EndsWith(".toc", StringComparison.OrdinalIgnoreCase))
                foreach (var m in ValidateToc(path)) Log(m);

            RefreshOutlineForActive();
        }

        private void SaveActiveTab()
        {
            if (EditorTabs.SelectedItem is TabItem tab) SaveTab(tab);
        }

        private void SaveTab(TabItem tab)
        {
            var path = tab.Tag as string;
            if (string.IsNullOrWhiteSpace(path)) return;
            if (tab.Content is TextEditor editor)
            {
                // Prepare text
                var original = editor.Text;
                var current = original;

                // 1) Optional: format via external tool
                if (_settings?.FormatOnSave == true &&
                    !string.IsNullOrWhiteSpace(_settings.LuaFormatterPath) &&
                    File.Exists(_settings.LuaFormatterPath))
                {
                    if (TryFormatText(current, path!, out var formatted, out var err))
                    {
                        current = formatted;
                    }
                    else if (!string.IsNullOrWhiteSpace(err))
                    {
                        Status("Format failed (see Output).");
                        Log(err);
                    }
                }

                // 2) Local save formatting (trim / ensure newline)
                current = ApplySaveFormatting(current);

                // If we changed the text, update the editor while preserving caret/scroll
                if (!string.Equals(original, current, StringComparison.Ordinal))
                {
                    // Preserve caret location
                    int caretLine = editor.TextArea.Caret.Line;
                    int caretCol = editor.TextArea.Caret.Column;

                    editor.Document.BeginUpdate();
                    try
                    {
                        editor.Document.Replace(0, editor.Document.TextLength, current);

                        caretLine = Math.Max(1, Math.Min(editor.Document.LineCount, caretLine));
                        var docLine = editor.Document.GetLineByNumber(caretLine);
                        int offset = editor.Document.GetOffset(caretLine, Math.Max(1, caretCol));
                        offset = Math.Max(docLine.Offset, Math.Min(docLine.EndOffset, offset));
                        editor.SelectionLength = 0;
                        editor.CaretOffset = offset;
                        editor.ScrollToLine(caretLine);
                    }
                    finally
                    {
                        editor.Document.EndUpdate();
                    }
                }

                // Write to disk
                File.WriteAllText(path!, editor.Text, Encoding.UTF8);
                FileWatch_NoteJustSaved(path!);

                MarkTabDirty(path!, false);
                Status($"Saved {Path.GetFileName(path)}");
                if (path!.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                    RefreshOutlineForActive();
            }
        }

        // ---------- EXTERNAL FORMATTER ----------

        private void FormatDocument_Click(object? sender, RoutedEventArgs e) => FormatActiveDocument();

        private void FormatActiveDocument()
        {
            Formatter_Autodetect();
            var ed = ActiveEditor();
            if (ed == null) { System.Media.SystemSounds.Beep.Play(); return; }

            if (_settings == null || string.IsNullOrWhiteSpace(_settings.LuaFormatterPath) || !File.Exists(_settings.LuaFormatterPath))
            {
                MessageBox.Show(this,
                    "No Lua formatter configured.\n\nUse Tools → Editor Preferences → “Set Lua formatter…” to pick an executable.",
                    "Format Document",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var filePath = EditorTabs.SelectedItem is TabItem t ? t.Tag as string : null;
            if (!TryFormatText(ed.Text, filePath, out var formatted, out var error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Log(error);
                    Status("Format failed (see Output).");
                    MessageBox.Show(this, "Formatting failed. Check the Output panel for details.", "Format Document", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return;
            }

            if (formatted == ed.Text)
            {
                Status("Format: no changes.");
                return;
            }

            // Preserve caret
            int caretLine = ed.TextArea.Caret.Line;
            int caretCol = ed.TextArea.Caret.Column;

            ed.Document.BeginUpdate();
            try
            {
                ed.Document.Replace(0, ed.Document.TextLength, formatted);

                caretLine = Math.Max(1, Math.Min(ed.Document.LineCount, caretLine));
                var docLine = ed.Document.GetLineByNumber(caretLine);
                int offset = ed.Document.GetOffset(caretLine, Math.Max(1, caretCol));
                offset = Math.Max(docLine.Offset, Math.Min(docLine.EndOffset, offset));
                ed.SelectionLength = 0;
                ed.CaretOffset = offset;
                ed.ScrollToLine(caretLine);
            }
            finally
            {
                ed.Document.EndUpdate();
            }

            Status("Format: applied.");
        }

        /// <summary>
        /// Run external formatter.
        /// If args contain {file}:
        ///   • in STDIN mode (args end with '-' or contain {stdin}), we substitute the **real active file path** (or a fake stdin.lua)
        ///     so tools like StyLua can locate config; the actual code is piped via STDIN.
        ///   • in non-STDIN mode, we write the buffer to a temp file and pass that temp path,
        ///     then read formatted output from stdout or the temp file (if tool edits in place).
        /// Placeholders: {path}, {dir}, {file}, {stdin}
        /// </summary>
        private bool TryFormatText(string input, string? activeFilePath, out string formatted, out string error)
        {
            formatted = input;
            error = string.Empty;

            try
            {
                var exe = _settings!.LuaFormatterPath!;
                var argsTemplate = _settings!.LuaFormatterArgs ?? DEFAULT_STYLUA_ARGS;

                string? dir = null;
                if (!string.IsNullOrWhiteSpace(activeFilePath))
                    dir = Path.GetDirectoryName(activeFilePath);

                bool stdinMode = ArgTemplateNeedsStdIn(argsTemplate);
                bool hasFilePh = argsTemplate.IndexOf("{file}", StringComparison.OrdinalIgnoreCase) >= 0;

                string args = argsTemplate
                    .Replace("{path}", activeFilePath ?? "")
                    .Replace("{dir}", dir ?? "");

                string? tempFile = null;

                if (stdinMode)
                {
                    // For {file}, pass the REAL file path if known, or a synthetic one.
                    if (hasFilePh)
                    {
                        string pathForArg = !string.IsNullOrWhiteSpace(activeFilePath)
                            ? activeFilePath!
                            : Path.Combine(dir ?? Environment.CurrentDirectory, "stdin.lua");
                        args = args.Replace("{file}", pathForArg);
                    }

                    // Remove optional {stdin} marker token (we only use it as a hint)
                    args = ReplaceCI(args, "{stdin}", string.Empty).Trim();
                }
                else
                {
                    // Non-STDIN mode: write buffer to temp file and pass it as {file}
                    if (hasFilePh)
                    {
                        tempFile = Path.Combine(Path.GetTempPath(), "wowide_fmt_" + Guid.NewGuid().ToString("N") + ".tmp");
                        File.WriteAllText(tempFile, input, Encoding.UTF8);
                        args = args.Replace("{file}", tempFile);
                    }

                    args = ReplaceCI(args, "{stdin}", string.Empty).Trim();
                }

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = dir ?? Environment.CurrentDirectory,
                    RedirectStandardInput = stdinMode,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();

                if (stdinMode)
                {
                    try
                    {
                        proc.StandardInput.Write(input);
                        proc.StandardInput.Flush();
                        proc.StandardInput.Close();
                    }
                    catch (Exception io)
                    {
                        string stderrEarly = proc.StandardError.ReadToEnd();
                        string stdoutEarly = proc.StandardOutput.ReadToEnd();
                        try { proc.WaitForExit(5000); } catch { /* ignore */ }

                        error = "Formatter closed its input early while writing to STDIN.\n\n" +
                                $"Arguments:\n{psi.FileName} {psi.Arguments}\n\n" +
                                $"Exception:\n{io}\n\n" +
                                (string.IsNullOrWhiteSpace(stderrEarly) ? "" : "Stderr:\n" + stderrEarly + "\n") +
                                (string.IsNullOrWhiteSpace(stdoutEarly) ? "" : "Stdout:\n" + stdoutEarly + "\n");
                        if (tempFile != null) { try { File.Delete(tempFile); } catch { } }
                        return false;
                    }
                }

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(15000);

                if (proc.ExitCode != 0)
                {
                    error = $"Formatter exited with code {proc.ExitCode}.\n\n" +
                            $"Arguments:\n{psi.FileName} {psi.Arguments}\n\n" +
                            (string.IsNullOrWhiteSpace(stderr) ? "" : "Stderr:\n" + stderr);
                    if (tempFile != null) { try { File.Delete(tempFile); } catch { } }
                    return false;
                }

                if (!stdinMode && string.IsNullOrEmpty(stdout) && tempFile != null && File.Exists(tempFile))
                {
                    // Some tools edit in place, so read the temp file result
                    stdout = File.ReadAllText(tempFile, Encoding.UTF8);
                }

                if (tempFile != null) { try { File.Delete(tempFile); } catch { } }

                formatted = string.IsNullOrEmpty(stdout) ? input : stdout;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
        }
        // Normalizes/repairs formatter args if they're missing or using old flags.
        private void Formatter_MigrateArgsIfNeeded()
        {
            if (_settings == null) return;

            string s = _settings.LuaFormatterArgs ?? string.Empty;

            // If unset OR using legacy/invalid flags, replace with our good default.
            bool looksLegacy =
                s.IndexOf("--stdin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.IndexOf("--stdout", StringComparison.OrdinalIgnoreCase) >= 0;

            if (string.IsNullOrWhiteSpace(s) || looksLegacy)
            {
                _settings.LuaFormatterArgs = DEFAULT_STYLUA_ARGS; // <-- from earlier: "--search-parent-directories --stdin-filepath \"{file}\" -"
                try { SettingsService.Save(_settings); } catch { /* ignore */ }
                Status("Formatter args normalized to default.");
            }
        }


        // ---------- SAVE-FORMATTING (trim, final newline) ----------

        private string ApplySaveFormatting(string text)
        {
            if (_settings == null) return text;

            string output = text;

            if (_settings.TrimTrailingWhitespaceOnSave)
                output = StripTrailingWhitespacePerLine(output);

            if (_settings.EnsureFinalNewlineOnSave)
                output = EnsureFinalNewline(output);

            return output;
        }

        private static string StripTrailingWhitespacePerLine(string text)
        {
            var sb = new StringBuilder(text.Length);
            int i = 0, len = text.Length;
            while (i < len)
            {
                int lineStart = i;

                while (i < len && text[i] != '\r' && text[i] != '\n') i++;
                int lineEnd = i;

                int trimEnd = lineEnd;
                while (trimEnd > lineStart)
                {
                    char c = text[trimEnd - 1];
                    if (c == ' ' || c == '\t') trimEnd--;
                    else break;
                }
                sb.Append(text, lineStart, trimEnd - lineStart);

                if (i < len)
                {
                    if (text[i] == '\r')
                    {
                        sb.Append('\r');
                        i++;
                        if (i < len && text[i] == '\n')
                        {
                            sb.Append('\n');
                            i++;
                        }
                    }
                    else if (text[i] == '\n')
                    {
                        sb.Append('\n');
                        i++;
                    }
                }
            }
            return sb.ToString();
        }

        private static string EnsureFinalNewline(string text)
        {
            if (text.Length == 0) return "\n";
            char last = text[^1];
            return (last == '\n' || last == '\r') ? text : text + Environment.NewLine;
        }

        private void MarkTabDirty(string path, bool dirty)
        {
            foreach (TabItem tab in EditorTabs.Items)
            {
                if (string.Equals(tab.Tag as string, path, StringComparison.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(path);
                    tab.Header = dirty ? name + "*" : name;
                    return;
                }
            }
        }

        private void ToggleComment(TextEditor ed)
        {
            var doc = ed.Document;
            var selStart = ed.SelectionStart;
            var selEnd = ed.SelectionStart + ed.SelectionLength;
            var startLine = doc.GetLineByOffset(selStart);
            var endLine = doc.GetLineByOffset(selEnd);

            using (doc.RunUpdate())
            {
                // First pass: check if all lines are already commented
                bool allCommented = true;
                for (var line = startLine; ; line = line.NextLine!)
                {
                    var text = doc.GetText(line.Offset, line.Length);
                    if (!text.TrimStart().StartsWith("--"))
                    {
                        allCommented = false;
                        break;
                    }
                    if (line == endLine) break;
                }

                // Second pass: comment or uncomment uniformly
                for (var line = startLine; ; line = line.NextLine!)
                {
                    var text = doc.GetText(line.Offset, line.Length);
                    int leading = text.Length - text.TrimStart().Length;

                    if (allCommented)
                    {
                        int idx = text.IndexOf("--", leading, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            int removeLen = 2;
                            if (idx + 2 < text.Length && text[idx + 2] == ' ') removeLen++;
                            doc.Remove(line.Offset + idx, removeLen);
                        }
                    }
                    else
                    {
                        doc.Insert(line.Offset + leading, "-- ");
                    }

                    if (line == endLine) break;
                }
            }
        }

        private void DuplicateLine(TextEditor ed)
        {
            var doc = ed.Document;
            var line = doc.GetLineByOffset(ed.CaretOffset);
            var text = doc.GetText(line.Offset, line.Length);
            doc.Insert(line.EndOffset, Environment.NewLine + text);
        }

        private static string GetWordAtOffset(string text, int offset)
        {
            if (offset < 0 || offset > text.Length) return "";
            int start = offset;
            int end = offset;

            bool IsWordChar(char c) =>
                char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == ':';

            if (start > 0 && !IsWordChar(text[start - 1]) && start < text.Length && IsWordChar(text[start])) end++;

            while (start > 0 && IsWordChar(text[start - 1])) start--;
            while (end < text.Length && IsWordChar(text[end])) end++;

            return start < end ? text.Substring(start, end - start) : "";
        }

        private void AttachEditorContextMenu(TextEditor ed, string filePath)
        {
            var cm = new ContextMenu();

            MenuItem MI(string header, RoutedEventHandler click, string? gesture = null)
            {
                var m = new MenuItem { Header = header };
                if (!string.IsNullOrWhiteSpace(gesture)) m.InputGestureText = gesture;
                m.Click += click;
                return m;
            }

            var miUndo = MI("Undo", (s, e) => ed.Undo(), "Ctrl+Z");
            var miRedo = MI("Redo", (s, e) => ed.Redo(), "Ctrl+Y");
            cm.Items.Add(miUndo);
            cm.Items.Add(miRedo);
            cm.Items.Add(new Separator());

            var miCut = MI("Cut", (s, e) => ed.Cut(), "Ctrl+X");
            var miCopy = MI("Copy", (s, e) => ed.Copy(), "Ctrl+C");
            var miPaste = MI("Paste", (s, e) => ed.Paste(), "Ctrl+V");
            var miSelAll = MI("Select All", (s, e) => ed.SelectAll(), "Ctrl+A");

            cm.Items.Add(miCut);
            cm.Items.Add(miCopy);
            cm.Items.Add(miPaste);
            cm.Items.Add(new Separator());
            cm.Items.Add(miSelAll);
            cm.Items.Add(new Separator());

            cm.Items.Add(MI("Go to Definition", (s, e) => GoToDefinition(ed), "F12"));
            cm.Items.Add(MI("Go to Line…", (s, e) => GoToLine_Prompt(), "Ctrl+G"));
            cm.Items.Add(MI("Find…", (s, e) => Find_Click(this, new RoutedEventArgs()), "Ctrl+F"));
            cm.Items.Add(MI("Find in Files…", (s, e) => FindInFiles_Click(this, new RoutedEventArgs()), "Ctrl+Shift+F"));
            cm.Items.Add(MI("Format Document", (s, e) => FormatActiveDocument(), "Ctrl+Shift+I"));
            cm.Items.Add(new Separator());

            cm.Items.Add(MI("Toggle Comment", (s, e) => ToggleComment(ed), "Ctrl+/"));
            cm.Items.Add(MI("Duplicate Line", (s, e) => DuplicateLine(ed), "Ctrl+D"));
            cm.Items.Add(new Separator());

            cm.Items.Add(MI("Toggle Word Wrap", (s, e) =>
            {
                ed.WordWrap = !ed.WordWrap;
                Status("Word wrap: " + (ed.WordWrap ? "ON" : "OFF"));
            }));

            cm.Items.Add(MI("Toggle Invisibles", (s, e) =>
            {
                bool flag = !ed.Options.ShowSpaces;
                ed.Options.ShowSpaces = flag;
                ed.Options.ShowTabs = flag;
                ed.Options.ShowEndOfLine = flag;
                Status("Invisibles: " + (flag ? "ON" : "OFF"));
            }));

            cm.Items.Add(new Separator());

            cm.Items.Add(MI("Open Containing Folder", (s, e) =>
            {
                try
                {
                    if (File.Exists(filePath))
                        Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                    else
                        Process.Start("explorer.exe", Path.GetDirectoryName(filePath) ?? ".");
                }
                catch { }
            }));

            cm.Items.Add(MI("Copy Full Path", (s, e) =>
            {
                try { Clipboard.SetText(filePath); } catch { }
            }));

            cm.Items.Add(MI("Copy Relative Path", (s, e) =>
            {
                try
                {
                    var rel = (_project != null)
                        ? Path.GetRelativePath(_project.RootPath, filePath)
                        : filePath;
                    Clipboard.SetText(rel);
                }
                catch { }
            }));

            cm.Opened += (s, e) =>
            {
                bool hasSel = !string.IsNullOrEmpty(ed.SelectedText);
                miCut.IsEnabled = hasSel;
                miCopy.IsEnabled = hasSel;

                var stack = ed.Document?.UndoStack;
                miUndo.IsEnabled = stack?.CanUndo ?? false;
                miRedo.IsEnabled = stack?.CanRedo ?? false;

                try { miPaste.IsEnabled = Clipboard.ContainsText(); }
                catch { miPaste.IsEnabled = true; }
            };

            ed.ContextMenu = cm;
        }

        private void Find_Click(object s, RoutedEventArgs e) => ShowFindWindow(false);
        private void Replace_Click(object s, RoutedEventArgs e) => ShowFindWindow(true);

        private void GoToLine_Click(object? sender, RoutedEventArgs e) => GoToLine_Prompt();

        // ---------------- Go To Line ----------------
        private void GoToLine_Prompt()
        {
            var ed = ActiveEditor();
            if (ed == null) { System.Media.SystemSounds.Beep.Play(); return; }

            int maxLine = ed.Document.LineCount;

            string input = Interaction.InputBox(
                $"Go to line (1–{maxLine}) or line:column",
                "Go To Line",
                (ed.TextArea.Caret.Line).ToString());

            if (string.IsNullOrWhiteSpace(input)) return;

            int line = ed.TextArea.Caret.Line;
            int column = ed.TextArea.Caret.Column;

            if (input.Contains(":"))
            {
                var parts = input.Split(':');
                if (!int.TryParse(parts[0].Trim(), out line)) return;
                if (parts.Length > 1 && !int.TryParse(parts[1].Trim(), out column)) column = 1;
            }
            else
            {
                if (!int.TryParse(input.Trim(), out line)) return;
                column = 1;
            }

            line = Math.Max(1, Math.Min(maxLine, line));
            if (column < 1) column = 1;

            var docLine = ed.Document.GetLineByNumber(line);
            int offset = ed.Document.GetOffset(line, column);
            offset = Math.Max(docLine.Offset, Math.Min(docLine.EndOffset, offset));

            ed.SelectionLength = 0;
            ed.CaretOffset = offset;
            ed.ScrollToLine(line);
            ed.TextArea.Focus();
            Status($"Go To: {line}:{column}");
        }

        private void FindInFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_project == null) { MessageBox.Show(this, "Open or create a project first."); return; }
            var dlg = new Windows.FindInFilesWindow { Owner = this, ProjectRoot = _project.RootPath };
            dlg.NavigateTo += (file, line, col) =>
            {
                OpenFileInTab(file);
                if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed)
                {
                    ed.ScrollToLine(line);
                    ed.CaretOffset = Math.Min(ed.Document.TextLength, ed.Document.GetOffset(line, Math.Max(col, 1)));
                    ed.Focus();
                }
            };
            dlg.ShowDialog();
        }

        // -------- Editor Preferences menu (toggle/commands) --------
        private void EditorPrefs_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            if (MiTrimOnSave != null) MiTrimOnSave.IsChecked = _settings.TrimTrailingWhitespaceOnSave;
            if (MiEnsureNewlineOnSave != null) MiEnsureNewlineOnSave.IsChecked = _settings.EnsureFinalNewlineOnSave;
            if (MiFormatOnSave != null) MiFormatOnSave.IsChecked = _settings.FormatOnSave;
        }

        private void MiTrimOnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null || MiTrimOnSave == null) return;
            _settings.TrimTrailingWhitespaceOnSave = MiTrimOnSave.IsChecked;
            TryPersistSettings();
            Status("Trim trailing whitespace on save: " + (_settings.TrimTrailingWhitespaceOnSave ? "ON" : "OFF"));
        }

        private void MiEnsureNewlineOnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null || MiEnsureNewlineOnSave == null) return;
            _settings.EnsureFinalNewlineOnSave = MiEnsureNewlineOnSave.IsChecked;
            TryPersistSettings();
            Status("Ensure newline at EOF: " + (_settings.EnsureFinalNewlineOnSave ? "ON" : "OFF"));
        }

        private void MiFormatOnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null || MiFormatOnSave == null) return;
            _settings.FormatOnSave = MiFormatOnSave.IsChecked;
            TryPersistSettings();
            Status("Format on save: " + (_settings.FormatOnSave ? "ON" : "OFF"));
        }

        private void EditorPrefs_SetFormatter_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;

            var ofd = new OpenFileDialog
            {
                Title = "Select Lua formatter executable",
                Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (ofd.ShowDialog(this) == true)
            {
                _settings.LuaFormatterPath = ofd.FileName;
                TryPersistSettings();
                Status("Lua formatter set: " + _settings.LuaFormatterPath);
            }
        }

        private void EditorPrefs_SetFormatterArgs_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;

            string current = _settings.LuaFormatterArgs ?? "";
            string msg = "Formatter arguments.\n\nPlaceholders: {path}, {dir}, {file}\n" +
                         "- If {file} is used, the editor writes the buffer to a temp file and passes that path.\n" +
                         "- If {file} is omitted, the editor uses STDIN/STDOUT.\n";
            string input = Interaction.InputBox(msg, "Formatter Args", current);
            if (!string.IsNullOrEmpty(input))
            {
                _settings.LuaFormatterArgs = input;
                TryPersistSettings();
                Status("Lua formatter args updated.");
            }
        }

        private void TryPersistSettings()
        {
            try { SaveSettings(); } catch { }
        }

        // -------- Bracket matching highlight --------
        private void AttachBracketHighlighting(TextEditor editor)
        {
            var renderer = new BracketHighlightRenderer(editor.TextArea.TextView);
            editor.TextArea.Caret.PositionChanged += (s, e) =>
            {
                var offset = editor.CaretOffset;
                var doc = editor.Document;
                if (doc == null) { renderer.ClearHighlight(); return; }

                var result = FindMatchingBracket(doc.Text, offset);
                if (result.HasValue)
                    renderer.SetHighlight(result.Value.Item1, result.Value.Item2);
                else
                    renderer.ClearHighlight();
            };
        }

        private static (int, int)? FindMatchingBracket(string text, int offset)
        {
            if (offset <= 0 || offset > text.Length) return null;

            // Check character before caret
            char ch = text[offset - 1];
            char match;
            bool forward;

            switch (ch)
            {
                case '(': match = ')'; forward = true; break;
                case ')': match = '('; forward = false; break;
                case '[': match = ']'; forward = true; break;
                case ']': match = '['; forward = false; break;
                case '{': match = '}'; forward = true; break;
                case '}': match = '{'; forward = false; break;
                default: return null;
            }

            int depth = 1;
            if (forward)
            {
                for (int i = offset; i < text.Length; i++)
                {
                    if (text[i] == ch) depth++;
                    else if (text[i] == match) depth--;
                    if (depth == 0) return (offset - 1, i);
                }
            }
            else
            {
                for (int i = offset - 2; i >= 0; i--)
                {
                    if (text[i] == ch) depth++;
                    else if (text[i] == match) depth--;
                    if (depth == 0) return (i, offset - 1);
                }
            }

            return null;
        }

        // -------- Auto-insert 'end' for Lua blocks --------
        private void AutoInsertLuaEnd(TextEditor editor)
        {
            var doc = editor.Document;
            var caret = editor.TextArea.Caret;
            var lineNum = caret.Line;
            if (lineNum < 1 || lineNum > doc.LineCount) return;

            var line = doc.GetLineByNumber(lineNum);
            var lineText = doc.GetText(line.Offset, line.Length).TrimEnd();

            // Get indentation
            var rawLine = doc.GetText(line.Offset, line.Length);
            int indent = 0;
            foreach (char c in rawLine)
            {
                if (c == ' ') indent++;
                else if (c == '\t') indent += 4;
                else break;
            }
            string indentStr = new string(' ', indent);
            string innerIndent = indentStr + new string(' ', editor.Options.IndentationSize);

            // Check if line ends with a block-opening keyword
            bool needsEnd = lineText.EndsWith("then") || lineText.EndsWith("do")
                || lineText.EndsWith("else") || lineText.EndsWith("repeat")
                || (lineText.Contains("function") && lineText.EndsWith(")"));

            if (needsEnd)
            {
                // Check if 'end' already exists below
                if (lineNum < doc.LineCount)
                {
                    var nextLine = doc.GetLineByNumber(lineNum + 1);
                    var nextText = doc.GetText(nextLine.Offset, nextLine.Length).Trim();
                    if (nextText == "end" || nextText.StartsWith("end ")) return;
                }

                // Will be inserted after Enter is processed — use Dispatcher
                editor.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
                {
                    var currentOffset = editor.CaretOffset;
                    var closer = lineText.EndsWith("repeat") ? "until " : "end";
                    var insertText = innerIndent + "\n" + indentStr + closer;
                    doc.Insert(currentOffset, insertText);
                    editor.CaretOffset = currentOffset + innerIndent.Length;
                });
            }
        }
    }

    /// <summary>
    /// Background renderer that highlights matching bracket pairs.
    /// </summary>
    internal class BracketHighlightRenderer : ICSharpCode.AvalonEdit.Rendering.IBackgroundRenderer
    {
        private readonly ICSharpCode.AvalonEdit.Rendering.TextView _textView;
        private int _pos1 = -1, _pos2 = -1;

        public BracketHighlightRenderer(ICSharpCode.AvalonEdit.Rendering.TextView textView)
        {
            _textView = textView;
            _textView.BackgroundRenderers.Add(this);
        }

        public void SetHighlight(int pos1, int pos2)
        {
            _pos1 = pos1;
            _pos2 = pos2;
            _textView.InvalidateLayer(Layer);
        }

        public void ClearHighlight()
        {
            _pos1 = -1;
            _pos2 = -1;
            _textView.InvalidateLayer(Layer);
        }

        public ICSharpCode.AvalonEdit.Rendering.KnownLayer Layer =>
            ICSharpCode.AvalonEdit.Rendering.KnownLayer.Selection;

        public void Draw(ICSharpCode.AvalonEdit.Rendering.TextView textView,
                         System.Windows.Media.DrawingContext drawingContext)
        {
            if (_pos1 < 0 || _pos2 < 0) return;

            var builder = new ICSharpCode.AvalonEdit.Rendering.BackgroundGeometryBuilder
            {
                CornerRadius = 1
            };

            var brush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xD7, 0x00));
            brush.Freeze();

            foreach (var pos in new[] { _pos1, _pos2 })
            {
                if (pos < 0 || pos >= textView.Document.TextLength) continue;
                var segment = new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset = pos, Length = 1 };
                builder.AddSegment(textView, segment);
            }

            var geometry = builder.CreateGeometry();
            if (geometry != null)
            {
                drawingContext.DrawGeometry(brush, null, geometry);
            }
        }
    }
}