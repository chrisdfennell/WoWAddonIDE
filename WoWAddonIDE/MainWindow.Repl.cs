// File: MainWindow.Repl.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private ReplEnvironment? _replEnv;
        private readonly List<string> _replHistory = new();
        private int _replHistoryIndex = -1;

        private void Repl_Init()
        {
            _replEnv = new ReplEnvironment();
            ReplOutput.AppendText("-- Lua REPL (MoonSharp) --\n");
            ReplOutput.AppendText("-- Type Lua expressions or statements. Up/Down for history.\n");
            ReplOutput.AppendText("-- WoW API stubs are available (CreateFrame, C_Timer, etc.)\n\n");
        }

        private async void Repl_Execute()
        {
            var input = ReplInput?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(input)) return;

            // Add to history
            _replHistory.Add(input);
            _replHistoryIndex = _replHistory.Count;

            ReplOutput.AppendText($"> {input}\n");
            ReplInput!.Clear();

            if (_replEnv == null) Repl_Init();

            // Run on background thread with timeout
            try
            {
                var env = _replEnv!;
                var task = Task.Run(() => env.Execute(input));
                var completed = await Task.WhenAny(task, Task.Delay(5000));

                if (completed == task)
                {
                    var (success, result) = task.Result;
                    if (!string.IsNullOrEmpty(result))
                        ReplOutput.AppendText(result + "\n");
                }
                else
                {
                    ReplOutput.AppendText("[Timeout] Execution exceeded 5 seconds.\n");
                }
            }
            catch (Exception ex)
            {
                ReplOutput.AppendText($"[Error] {ex.Message}\n");
            }

            ReplOutput.AppendText("\n");
            ReplOutput.ScrollToEnd();
        }

        private async void Repl_LoadFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load Lua file into REPL",
                Filter = "Lua Files (*.lua)|*.lua|All Files|*.*",
                InitialDirectory = _project?.RootPath ?? Environment.CurrentDirectory
            };

            if (dlg.ShowDialog(this) != true) return;

            if (_replEnv == null) Repl_Init();

            try
            {
                var env = _replEnv!;
                var path = dlg.FileName;
                var task = Task.Run(() => env.LoadFile(path));
                var (success, result) = await task;

                ReplOutput.AppendText(result + "\n\n");
                ReplOutput.ScrollToEnd();
            }
            catch (Exception ex)
            {
                ReplOutput.AppendText($"[Error] {ex.Message}\n\n");
                ReplOutput.ScrollToEnd();
            }
        }

        private void Repl_Reset_Click(object sender, RoutedEventArgs e)
        {
            _replEnv?.Reset();
            ReplOutput.Clear();
            Repl_Init();
        }

        private void Repl_Clear_Click(object sender, RoutedEventArgs e)
        {
            ReplOutput.Clear();
        }

        private void ReplInput_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    Repl_Execute();
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (_replHistory.Count > 0 && _replHistoryIndex > 0)
                    {
                        _replHistoryIndex--;
                        ReplInput.Text = _replHistory[_replHistoryIndex];
                        ReplInput.CaretIndex = ReplInput.Text.Length;
                    }
                    e.Handled = true;
                    break;

                case Key.Down:
                    if (_replHistoryIndex < _replHistory.Count - 1)
                    {
                        _replHistoryIndex++;
                        ReplInput.Text = _replHistory[_replHistoryIndex];
                        ReplInput.CaretIndex = ReplInput.Text.Length;
                    }
                    else
                    {
                        _replHistoryIndex = _replHistory.Count;
                        ReplInput.Text = "";
                    }
                    e.Handled = true;
                    break;
            }
        }

        private void Repl_Run_Click(object sender, RoutedEventArgs e)
        {
            Repl_Execute();
        }
    }
}
