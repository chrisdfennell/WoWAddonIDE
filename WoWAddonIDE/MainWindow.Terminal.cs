// File: MainWindow.Terminal.cs
using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private Process? _terminalProcess;

        private void Terminal_Run_Click(object sender, RoutedEventArgs e)
        {
            Terminal_Execute();
        }

        private void TerminalInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Terminal_Execute();
                e.Handled = true;
            }
        }

        private void Terminal_Execute()
        {
            var cmd = TerminalInput?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(cmd)) return;

            TerminalOutput.AppendText($"> {cmd}\n");
            TerminalInput!.Clear();

            var workDir = _project?.RootPath ?? Environment.CurrentDirectory;

            try { _terminalProcess?.Kill(); } catch { /* ignore */ }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {cmd}",
                    WorkingDirectory = workDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _terminalProcess = proc;

                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        Dispatcher.BeginInvoke(() =>
                        {
                            TerminalOutput.AppendText(e.Data + "\n");
                            TerminalOutput.ScrollToEnd();
                        });
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                        Dispatcher.BeginInvoke(() =>
                        {
                            TerminalOutput.AppendText("[ERR] " + e.Data + "\n");
                            TerminalOutput.ScrollToEnd();
                        });
                };

                proc.Exited += (s, e) =>
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        TerminalOutput.AppendText($"[exit {proc.ExitCode}]\n");
                        TerminalOutput.ScrollToEnd();
                    });
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                TerminalOutput.AppendText($"[Error] {ex.Message}\n");
            }
        }
    }
}
