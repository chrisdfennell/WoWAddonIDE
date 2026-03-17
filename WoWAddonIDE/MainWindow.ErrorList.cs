using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        /// <summary>View-model row for the Error List panel.</summary>
        public class ErrorListItem
        {
            public string Severity { get; set; } = "";
            public string FileName { get; set; } = "";
            public int Line { get; set; }
            public string Message { get; set; } = "";
            public string FullPath { get; set; } = "";
        }

        private List<ErrorListItem> _allDiagnostics = new();

        /// <summary>Runs lint on the current project and populates the Error List.</summary>
        private void ErrorList_RunLint_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureProject()) return;
            RunLintAndPopulateErrorList();
        }

        internal void RunLintAndPopulateErrorList()
        {
            if (_project == null) return;

            var diags = LuaLint.Analyze(_project);

            // Also validate all TOC files
            var tocFiles = TocParser.DiscoverTocFiles(_project.RootPath, _project.Name);
            foreach (var (label, tocPath) in tocFiles)
            {
                foreach (var msg in ValidateToc(tocPath))
                {
                    diags.Add(new LuaLint.LintDiagnostic
                    {
                        File = tocPath,
                        Severity = msg.StartsWith("Missing required") ? "error" : "warning",
                        Message = msg
                    });
                }
            }

            _allDiagnostics = diags.Select(d => new ErrorListItem
            {
                Severity = d.Severity,
                FileName = string.IsNullOrWhiteSpace(d.File) ? "" : Path.GetFileName(d.File),
                Line = d.Line,
                Message = d.Message,
                FullPath = d.File
            }).ToList();

            ApplyErrorListFilter();

            // Switch to Error List tab
            BottomTabs.SelectedItem = ErrorListTab;

            var errorCount = _allDiagnostics.Count(d => d.Severity == "error");
            var warnCount = _allDiagnostics.Count(d => d.Severity == "warning");
            var infoCount = _allDiagnostics.Count(d => d.Severity == "info");
            Log($"Lint: {errorCount} error(s), {warnCount} warning(s), {infoCount} info");
        }

        private void ErrorList_Clear_Click(object sender, RoutedEventArgs e)
        {
            _allDiagnostics.Clear();
            ErrorListView.ItemsSource = null;
            UpdateErrorListTabHeader(0);
        }

        private void ErrorList_FilterChanged(object sender, RoutedEventArgs e)
        {
            ApplyErrorListFilter();
        }

        private void ApplyErrorListFilter()
        {
            bool showErrors = ErrorListShowErrors?.IsChecked == true;
            bool showWarnings = ErrorListShowWarnings?.IsChecked == true;
            bool showInfo = ErrorListShowInfo?.IsChecked == true;

            var filtered = _allDiagnostics.Where(d =>
                (d.Severity == "error" && showErrors) ||
                (d.Severity == "warning" && showWarnings) ||
                (d.Severity == "info" && showInfo))
                .ToList();

            ErrorListView.ItemsSource = filtered;
            UpdateErrorListTabHeader(filtered.Count);
        }

        private void UpdateErrorListTabHeader(int count)
        {
            ErrorListTab.Header = $"Error List ({count})";
        }

        /// <summary>Double-click a diagnostic to navigate to the file and line.</summary>
        private void ErrorListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ErrorListView.SelectedItem is not ErrorListItem item) return;
            if (string.IsNullOrWhiteSpace(item.FullPath) || !File.Exists(item.FullPath)) return;

            OpenFileInTab(item.FullPath);

            // Navigate to the line
            if (item.Line > 0 && TryGetActiveEditor(out var ed, out _))
            {
                var line = Math.Min(item.Line, ed.Document.LineCount);
                var docLine = ed.Document.GetLineByNumber(line);
                ed.CaretOffset = docLine.Offset;
                ed.ScrollToLine(line);
                ed.Focus();
            }
        }
    }
}
