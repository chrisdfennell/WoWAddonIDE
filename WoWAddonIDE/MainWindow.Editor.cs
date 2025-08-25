using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
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

        private void OpenFileInTab(string path)
        {
            foreach (TabItem tab in EditorTabs.Items)
            {
                if ((tab.Tag as string) == path)
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

            var tabItem = new TabItem
            {
                Header = Path.GetFileName(path),
                Content = editor,
                Tag = path
            };

            EditorTabs.Items.Add(tabItem);
            EditorTabs.SelectedItem = tabItem;

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
                File.WriteAllText(path, editor.Text);
                MarkTabDirty(path, false);
                Status($"Saved {Path.GetFileName(path)}");
                if (path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                    RefreshOutlineForActive();
            }
        }

        private void MarkTabDirty(string path, bool dirty)
        {
            foreach (TabItem tab in EditorTabs.Items)
            {
                if ((tab.Tag as string) == path)
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
                var line = startLine;
                while (true)
                {
                    var text = doc.GetText(line.Offset, line.Length);
                    var trimmed = text.TrimStart();
                    var leading = text.Length - trimmed.Length;
                    if (trimmed.StartsWith("--"))
                    {
                        var idxInLine = leading + trimmed.IndexOf("--", StringComparison.Ordinal);
                        doc.Remove(line.Offset + idxInLine, 2);
                    }
                    else
                    {
                        doc.Insert(line.Offset + leading, "--");
                    }

                    if (line == endLine) break;
                    line = line.NextLine!;
                }
            }
        }

        private void DuplicateLine(TextEditor ed)
        {
            var doc = ed.Document;
            var line = doc.GetLineByOffset(ed.CaretOffset);
            var text = doc.GetText(line.Offset, line.TotalLength);
            doc.Insert(line.EndOffset, text);
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

            MenuItem MI(string header, RoutedEventHandler click, string gesture = null)
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
            cm.Items.Add(MI("Find…", (s, e) => Find_Click(this, new RoutedEventArgs()), "Ctrl+F"));
            cm.Items.Add(MI("Find in Files…", (s, e) => FindInFiles_Click(this, new RoutedEventArgs()), "Ctrl+Shift+F"));
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

        private void Find_Click(object s, RoutedEventArgs e)
        {
            if (EditorTabs.SelectedItem is TabItem tab && tab.Content is TextEditor ed)
            {
                var q = Interaction.InputBox("Find text:", "Find", ed.SelectedText ?? "");
                if (!string.IsNullOrEmpty(q))
                {
                    var idx = ed.Text.IndexOf(q, ed.CaretOffset, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) idx = ed.Text.IndexOf(q, 0, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        ed.Select(idx, q.Length);
                        ed.ScrollToLine(ed.Document.GetLineByOffset(idx).LineNumber);
                        ed.Focus();
                        Status($"Found: {q}");
                    }
                    else Status($"'{q}' not found");
                }
            }
            else
            {
                FindInFiles_Click(s, e);
            }
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
    }
}