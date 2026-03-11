using ICSharpCode.AvalonEdit;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using static WoWAddonIDE.MainWindow;

namespace WoWAddonIDE.Windows
{
    public partial class FindReplaceWindow : Window
    {
        private readonly Func<TextEditor?> _getActiveEditor;
        private readonly Func<System.Collections.Generic.IEnumerable<TextEditor>> _getAllEditors;

        // --- Add to FindReplaceWindow class ---

        // Save current inputs/options so host-driven finds use up-to-date values
        private void SaveState()
        {
            // Later add static state for headless finds, update it here.
            // For now, we just ensure the instance state (controls) is current.
        }

        // Public hooks the host (MainWindow) can call without focusing this window
        public void FindNextFromHost()
        {
            SaveState();
            FindNext();   // your existing private method
        }

        public void FindPrevFromHost()
        {
            SaveState();
            FindPrev();   // your existing private method
        }

        public void FocusReplaceFromHost()
        {
            ReplaceBox.Focus();
            ReplaceBox.SelectAll();
        }


        public FindReplaceWindow(Func<TextEditor?> getActiveEditor,
                                 Func<System.Collections.Generic.IEnumerable<TextEditor>> getAllEditors,
                                 string? seedFind = null,
                                 bool focusReplace = false)
        {
            InitializeComponent();
            _getActiveEditor = getActiveEditor;
            _getAllEditors = getAllEditors;

            if (!string.IsNullOrEmpty(seedFind))
                FindBox.Text = seedFind;

            // Shortcuts within the dialog
            this.InputBindings.Add(new KeyBinding(new RelayCommand(_ => FindNext()), new KeyGesture(Key.F3)));
            this.InputBindings.Add(new KeyBinding(new RelayCommand(_ => FindPrev()), new KeyGesture(Key.F3, ModifierKeys.Shift)));
            this.InputBindings.Add(new KeyBinding(new RelayCommand(_ => ReplaceOne()), new KeyGesture(Key.H, ModifierKeys.Control)));
            this.InputBindings.Add(new KeyBinding(new RelayCommand(_ => this.Close()), new KeyGesture(Key.Escape)));

            if (focusReplace) ReplaceBox.Focus(); else FindBox.Focus();
            FindBox.SelectAll();
        }

        // ----- Buttons
        private void BtnFindNext_Click(object s, RoutedEventArgs e) => FindNext();
        private void BtnFindPrev_Click(object s, RoutedEventArgs e) => FindPrev();
        private void BtnReplace_Click(object s, RoutedEventArgs e) => ReplaceOne();
        private void BtnReplaceAll_Click(object s, RoutedEventArgs e) => ReplaceAll();
        private void BtnClose_Click(object s, RoutedEventArgs e) => this.Close();

        // ----- Core
        private void FindNext() => DoFind(forward: true, selectResult: true);
        private void FindPrev() => DoFind(forward: false, selectResult: true);

        private void ReplaceOne()
        {
            var ed = _getActiveEditor();
            if (ed == null) return;

            if (ed.SelectionLength > 0 &&
                Matches(ed.SelectedText, FindBox.Text, ChkRegex.IsChecked == true, ChkMatchCase.IsChecked == true, ChkWholeWord.IsChecked == true))
            {
                ed.Document.Replace(ed.SelectionStart, ed.SelectionLength, ReplaceBox.Text ?? "");
            }
            // After replacing current selection, move to next match
            DoFind(forward: true, selectResult: true);
            ed.Focus();
        }

        private void ReplaceAll()
        {
            var ed = _getActiveEditor();
            if (ed == null) return;

            string find = FindBox.Text ?? "";
            if (string.IsNullOrEmpty(find)) return;

            int replacements = 0;

            if (ChkRegex.IsChecked == true)
            {
                var opts = RegexOptions.Multiline | RegexOptions.CultureInvariant;
                if (ChkMatchCase.IsChecked != true) opts |= RegexOptions.IgnoreCase;
                var rx = new Regex(WholeWordPattern(find, useRegex: true), opts);

                var range = GetSearchRange(ed, ChkSelection.IsChecked == true);
                var text = ed.Document.GetText(range.start, range.length);
                var replaced = rx.Replace(text, ReplaceBox.Text ?? "");
                if (!ReferenceEquals(replaced, text))
                {
                    // Count replacements via Matches
                    replacements = rx.Matches(text).Count;
                    ed.Document.Replace(range.start, range.length, replaced);
                }
            }
            else
            {
                var range = GetSearchRange(ed, ChkSelection.IsChecked == true);
                var comparison = ChkMatchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                string needle = find;
                if (ChkWholeWord.IsChecked == true) needle = WholeWordPattern(find, useRegex: false); // guard via manual checks

                // Simple loop replace
                int i = range.start;
                while (i <= range.start + range.length - 1)
                {
                    int hit = ed.Document.Text.IndexOf(find, i, comparison);
                    if (hit < 0 || hit >= range.start + range.length) break;

                    if (ChkWholeWord.IsChecked == true && !IsWholeWordBoundary(ed.Document.Text, hit, find.Length))
                    { i = hit + 1; continue; }

                    ed.Document.Replace(hit, find.Length, ReplaceBox.Text ?? "");
                    replacements++;
                    i = hit + (ReplaceBox.Text ?? "").Length;
                }
            }

            MessageBox.Show(this, $"Replaced {replacements} occurrence(s).", "Replace All");
        }

        private void DoFind(bool forward, bool selectResult)
        {
            var ed = _getActiveEditor();
            if (ed == null) return;

            string find = FindBox.Text ?? "";
            if (string.IsNullOrEmpty(find)) return;

            var (start, length) = GetSearchRange(ed, ChkSelection.IsChecked == true);

            int caret = ed.CaretOffset;
            int nextFrom = forward ? Math.Max(caret + (ed.SelectionLength > 0 ? 1 : 0), start) : Math.Min(caret - 1, start + length - 1);

            if (ChkRegex.IsChecked == true)
            {
                var opts = RegexOptions.Multiline | RegexOptions.CultureInvariant;
                if (ChkMatchCase.IsChecked != true) opts |= RegexOptions.IgnoreCase;
                var rx = new Regex(WholeWordPattern(find, useRegex: true), opts);

                // Forward/backward search using Matches
                var text = ed.Document.GetText(start, length);
                Match? best = null;
                foreach (Match m in rx.Matches(text))
                {
                    int abs = start + m.Index;
                    if (forward)
                    {
                        if (abs >= nextFrom) { best = m; break; }
                    }
                    else
                    {
                        if (abs < ed.CaretOffset && (best == null || m.Index > best.Index)) best = m;
                    }
                }

                if (best == null && ChkWrap.IsChecked == true)
                {
                    // wrap
                    foreach (Match m in rx.Matches(text))
                    {
                        int abs = start + m.Index;
                        if (forward) { best = m; break; }
                        else best = m; // last match
                    }
                }

                if (best != null && selectResult)
                {
                    ed.Select(start + best.Index, best.Length);
                    ed.ScrollToLine(ed.Document.GetLineByOffset(ed.SelectionStart).LineNumber);
                }
            }
            else
            {
                var txt = ed.Document.Text;
                var comparison = ChkMatchCase.IsChecked == true ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                int hit = -1;
                if (forward)
                {
                    int from = Math.Max(nextFrom, start);
                    while (true)
                    {
                        hit = txt.IndexOf(find, from, comparison);
                        if (hit < 0 || hit >= start + length) break;
                        if (ChkWholeWord.IsChecked == true && !IsWholeWordBoundary(txt, hit, find.Length))
                        { from = hit + 1; continue; }
                        break;
                    }
                    if ((hit < 0 || hit >= start + length) && ChkWrap.IsChecked == true)
                    {
                        from = start;
                        while (true)
                        {
                            hit = txt.IndexOf(find, from, comparison);
                            if (hit < 0 || hit >= start + length) break;
                            if (ChkWholeWord.IsChecked == true && !IsWholeWordBoundary(txt, hit, find.Length))
                            { from = hit + 1; continue; }
                            break;
                        }
                    }
                }
                else
                {
                    int from = Math.Min(nextFrom, start + length - 1);
                    while (true)
                    {
                        hit = txt.LastIndexOf(find, from, from - start + 1, comparison);
                        if (hit < 0) break;
                        if (hit < start) break;
                        if (ChkWholeWord.IsChecked == true && !IsWholeWordBoundary(txt, hit, find.Length))
                        { from = hit - 1; continue; }
                        break;
                    }
                    if ((hit < 0 || hit < start) && ChkWrap.IsChecked == true)
                    {
                        from = start + length - 1;
                        while (true)
                        {
                            hit = txt.LastIndexOf(find, from, from - start + 1, comparison);
                            if (hit < 0 || hit < start) break;
                            if (ChkWholeWord.IsChecked == true && !IsWholeWordBoundary(txt, hit, find.Length))
                            { from = hit - 1; continue; }
                            break;
                        }
                    }
                }

                if (hit >= start && hit < start + length && selectResult)
                {
                    ed.Select(hit, find.Length);
                    ed.ScrollToLine(ed.Document.GetLineByOffset(hit).LineNumber);
                }
            }

            ed.Focus();
        }

        // ----- Helpers
        private static bool Matches(string text, string pattern, bool regex, bool matchCase, bool wholeWord)
        {
            if (regex)
            {
                var opts = RegexOptions.Multiline | RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
                return Regex.IsMatch(text, WholeWordPattern(pattern, useRegex: true), opts);
            }
            var cmp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            if (!text.Contains(pattern, cmp)) return false;
            if (wholeWord) return IsWholeWordBoundary(text, text.IndexOf(pattern, 0, cmp), pattern.Length);
            return true;
        }

        private (int start, int length) GetSearchRange(TextEditor ed, bool selectionOnly)
        {
            if (selectionOnly && ed.SelectionLength > 0)
                return (ed.SelectionStart, ed.SelectionLength);
            return (0, ed.Document.TextLength);
        }

        private static bool IsWholeWordBoundary(string text, int startIndex, int len)
        {
            bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.';
            int before = startIndex - 1;
            int after = startIndex + len;
            bool leftOk = before < 0 || !IsWordChar(text[before]);
            bool rightOk = after >= text.Length || !IsWordChar(text[after]);
            return leftOk && rightOk;
        }

        private static string WholeWordPattern(string input, bool useRegex)
        {
            if (!useRegex) return input; // we enforce whole-word manually in non-regex path
            // If regex, wrap with \b where possible; allow Lua identifiers incl. dot
            return $@"(?<![\w\.])(?:{input})(?![\w\.])";
        }
    }
}