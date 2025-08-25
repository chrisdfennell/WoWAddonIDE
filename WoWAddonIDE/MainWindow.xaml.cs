using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;
using WoWAddonIDE.Models;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private AddonProject? _project;
        private readonly string _settingsPath;
        private IDESettings _settings;

        // Editor toggles (session-scoped)
        private bool _wordWrap = false;
        private bool _showInvisibles = false;

        private System.Windows.Threading.DispatcherTimer? _gitStatusTimer;

        private CompletionService _completion;

        // Hover docs (from Resources/wow_api.json)
        private Dictionary<string, ApiEntry> _apiDocs = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, List<SymbolService.SymbolLocation>> _symbolIndex = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _symbolIndexBuilt = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();

            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WoWAddonIDE", "settings.json");

            _settings = LoadSettings();

            this.InputBindings.Add(
                new KeyBinding(
                    new RelayCommand(_ => OpenHelpWindow()),
                    new KeyGesture(Key.F1)));

            // Initialize completion/highlighting (loads wow_api.json for completion)
            _completion = new CompletionService();

            // Register Lua & TOC highlighting
            EnsureLuaHighlightRegistered();
            EnsureTocHighlightRegistered();

            // Load API docs for hover tooltips
            LoadApiDocs();

            Status("Ready");
            if (string.IsNullOrWhiteSpace(_settings.AddOnsPath))
            {
                _settings.AddOnsPath = DetectAddOnsPath();
                SaveSettings();
            }

            PathText.Text = string.IsNullOrWhiteSpace(_settings.AddOnsPath)
                ? "AddOns Path: (not set) — Tools > Settings..."
                : $"AddOns Path: {_settings.AddOnsPath}";

            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => OpenCommandPalette_Click(this, new RoutedEventArgs())),
                new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)));


            // Sanity logs
            if (_completion.LuaHighlight == null && HighlightingManager.Instance.GetDefinition("Lua") == null)
                Log("Lua highlight NOT available — check Resources/Lua.xshd (Build Action: Resource) and XML.");
            else
                Log("Lua highlight is available.");

            // Global keybinding: Find in Files
            this.InputBindings.Add(new KeyBinding(
                new RelayCommand(_ => FindInFiles_Click(this, new RoutedEventArgs())),
                new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Shift)));

            // THEME: initialize + apply + listen for changes
            ThemeManager.Initialize(_settings);
            ThemeManager.Persist = SaveSettings;
            ThemeManager.ApplyTheme(_settings.ThemeMode); // System/Light/Dark

            ThemeManager.ThemeChanged += () =>
            {
                foreach (TabItem tab in EditorTabs.Items)
                    if (tab.Content is TextEditor ed)
                    {
                        ThemeManager.ApplyToEditor(ed);
                        if (ed.SyntaxHighlighting != null)
                            RetintHighlighting(ed.SyntaxHighlighting, IsDarkThemeActive());
                    }
            };
        }

        private void Log(string text)
        {
            Output.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
            Output.ScrollToEnd();
        }

        private void Status(string text) => StatusText.Text = text;

        private bool EnsureProject()
        {
            if (_project == null)
            {
                MessageBox.Show(this, "Open or create a project first.", "No Project", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            return true;
        }

        // Small command helper for keybindings
        public class RelayCommand : ICommand
        {
            private readonly Action<object?> _exec; private readonly Func<object?, bool>? _can;
            public RelayCommand(Action<object?> exec, Func<object?, bool>? can = null) { _exec = exec; _can = can; }
            public bool CanExecute(object? p) => _can?.Invoke(p) ?? true;
            public void Execute(object? p) => _exec(p);
            public event EventHandler? CanExecuteChanged { add { } remove { } }
        }
    }
}