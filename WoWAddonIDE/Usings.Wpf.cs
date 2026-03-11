// Usings.Wpf.cs  — top-level, no namespace/class

// Keep these global aliases small and WPF-only:
global using Application = System.Windows.Application;
global using MessageBox = System.Windows.MessageBox;
global using ToolTip = System.Windows.Controls.ToolTip;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using Clipboard = System.Windows.Clipboard;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;

// IMPORTANT: do NOT alias Color or Brushes globally — they conflict with System.Drawing.