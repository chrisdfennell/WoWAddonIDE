using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml;

namespace WoWAddonIDE.Services
{
    public class WoWApiEntry
    {
        public string name { get; set; } = "";
        public string signature { get; set; } = "";
        public string description { get; set; } = "";
    }

    /// <summary>
    /// Loads Lua highlighting, WoW API metadata, and creates completion/insight popups.
    /// </summary>
    public class CompletionService
    {
        private readonly string[] _luaKeywords =
        {
            "and","break","do","else","elseif","end","false","for","function",
            "goto","if","in","local","nil","not","or","repeat","return","then",
            "true","until","while","pairs","ipairs","require","pcall","xpcall","type","table","string","math","debug"
        };

        private List<WoWApiEntry> _api = new();

        // NEW: fast set of API *names* you can import dynamically (no signatures/desc required)
        private HashSet<string> _apiNames = new(StringComparer.OrdinalIgnoreCase);

        public IHighlightingDefinition? LuaHighlight { get; private set; }

        public CompletionService()
        {
            LuaHighlight = LoadLuaHighlight();
            _api = LoadWoWApi();
            // Seed _apiNames from bundled data (so they’re included even before imports)
            _apiNames.UnionWith(_api.Select(a => a.name));
        }

        // Allow MainWindow to inject API names parsed from user-imported JSON
        public void SetApiDocs(IEnumerable<string> apiNames)
        {
            _apiNames = new HashSet<string>(apiNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            // Also keep any names from built-in docs so suggestions don’t regress
            _apiNames.UnionWith(_api.Select(a => a.name));
        }

        private static IHighlightingDefinition? LoadLuaHighlight()
        {
            try
            {
                Stream? TryOpen()
                {
                    // 1) WPF resource relative
                    var s = Application.GetResourceStream(new Uri("Resources/Lua.xshd", UriKind.Relative))?.Stream;
                    if (s != null) return s;

                    // 2) Pack URI (replace WoWAddonIDE with your assembly name if different)
                    s = Application.GetResourceStream(new Uri("/WoWAddonIDE;component/Resources/Lua.xshd", UriKind.Relative))?.Stream;
                    if (s != null) return s;

                    // 3) File next to exe (handy if you want to just drop it in bin/)
                    var disk = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Lua.xshd");
                    if (File.Exists(disk)) return File.OpenRead(disk);

                    // 4) Embedded resource (if someone set EmbeddedResource)
                    var asm = typeof(CompletionService).Assembly;
                    var resName = asm.GetManifestResourceNames().FirstOrDefault(n =>
                        n.EndsWith("Lua.xshd", StringComparison.OrdinalIgnoreCase));
                    if (resName != null) return asm.GetManifestResourceStream(resName);

                    return null;
                }

                using var stream = TryOpen();
                if (stream == null) return null;

                using var reader = new XmlTextReader(stream);
                var def = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(reader, HighlightingManager.Instance);

                // Register so it’s also accessible by name/extension
                HighlightingManager.Instance.RegisterHighlighting("Lua", new[] { ".lua" }, def);
                return def;
            }
            catch
            {
                return null;
            }
        }

        private static List<WoWApiEntry> LoadWoWApi()
        {
            try
            {
                using var s = Application.GetResourceStream(new Uri("Resources/wow_api.json", UriKind.Relative))?.Stream;
                if (s == null) return new List<WoWApiEntry>();
                using var sr = new StreamReader(s);
                var json = sr.ReadToEnd();
                return JsonConvert.DeserializeObject<List<WoWApiEntry>>(json) ?? new List<WoWApiEntry>();
            }
            catch { return new List<WoWApiEntry>(); }
        }

        /// <summary>
        /// Show a CompletionWindow with Lua keywords, WoW APIs, imported API names, and buffer words filtered by prefix.
        /// </summary>
        public void ShowCompletion(TextArea area, string currentWord)
        {
            if (area?.Document == null) return;

            // buffer identifiers
            var bufferWords = ExtractIdentifiers(area.Document)
                .Where(w => w.Length >= 3)
                .Take(500);

            var apiNamesFromEntries = _api.Select(a => a.name);

            var candidates = _luaKeywords
                .Concat(apiNamesFromEntries)
                .Concat(_apiNames)        // <- imported names merged here
                .Concat(bufferWords)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(w => w.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                .OrderBy(w => w, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count == 0) return;

            var cw = new CompletionWindow(area);
            var data = cw.CompletionList.CompletionData;

            foreach (var c in candidates)
            {
                var api = _api.FirstOrDefault(a => a.name.Equals(c, StringComparison.OrdinalIgnoreCase));
                if (api != null)
                    data.Add(new RichCompletionData(api.name, api.signature, api.description));
                else
                    data.Add(new RichCompletionData(c, c, "Identifier"));
            }

            // Snippets (typed triggers start with '!')
            if ("!slash".StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                data.Add(Snippets.SlashCommandSnippet());
            if ("!ace".StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                data.Add(Snippets.Ace3Snippet());

            cw.Show();
        }

        /// <summary>
        /// Show a simple parameter-hints window if function name is known.
        /// </summary>
        public void ShowParameterHints(TextArea area, string functionName)
        {
            if (area == null) return;

            var api = _api.FirstOrDefault(a => a.name.Equals(functionName, StringComparison.OrdinalIgnoreCase));
            if (api == null) return;

            var iw = new OverloadInsightWindow(area)
            {
                Provider = new SingleOverloadProvider(api.signature, api.description)
            };
            iw.Show();
        }

        public static string GetCurrentWord(TextArea area)
        {
            var caret = area.Caret.Offset;
            var doc = area.Document;
            int start = caret;
            while (start > 0)
            {
                var ch = doc.GetCharAt(start - 1);
                if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '!' && ch != '.') break;
                start--;
            }
            return doc.GetText(start, caret - start);
        }

        public static string GetWordBeforeChar(TextArea area, char triggerChar)
        {
            var caret = area.Caret.Offset;
            var doc = area.Document;

            // word immediately before the '('
            int pos = caret - 2; // caret is after '('; step to char before it
            if (pos < 0) return "";
            while (pos >= 0 && (char.IsWhiteSpace(doc.GetCharAt(pos)) || doc.GetCharAt(pos) == triggerChar))
                pos--;

            int end = pos + 1;
            while (pos >= 0)
            {
                var ch = doc.GetCharAt(pos);
                if (!char.IsLetterOrDigit(ch) && ch != '_' && ch != '.') break;
                pos--;
            }
            int start = pos + 1;
            if (end <= start) return "";
            return doc.GetText(start, end - start);
        }

        private static IEnumerable<string> ExtractIdentifiers(TextDocument doc)
        {
            var text = doc.Text;
            var id = new System.Text.StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    id.Append(ch);
                }
                else
                {
                    if (id.Length > 0)
                    {
                        yield return id.ToString();
                        id.Clear();
                    }
                }
            }
            if (id.Length > 0) yield return id.ToString();
        }
    }

    // --- Completion item with nice tooltip ---
    public class RichCompletionData : ICompletionData
    {
        public RichCompletionData(string text, string signature, string description)
        {
            Text = text;
            Content = text;
            Description = $"{signature}\n\n{description}";
        }

        public System.Windows.Media.ImageSource? Image => null;
        public string Text { get; }
        public object Content { get; }
        public object Description { get; }
        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }

    // --- One-overload provider for parameter hints ---
    public class SingleOverloadProvider : IOverloadProvider
    {
        private readonly string _header;
        private readonly string _desc;
        private int _selectedIndex;

        public SingleOverloadProvider(string header, string desc)
        {
            _header = header;
            _desc = desc;
            _selectedIndex = 0;
        }

        // INotifyPropertyChanged (required by IOverloadProvider)
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // IOverloadProvider members
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value == _selectedIndex) return;
                _selectedIndex = value;
                // notify dependent properties as well
                OnPropertyChanged(nameof(SelectedIndex));
                OnPropertyChanged(nameof(CurrentHeader));
                OnPropertyChanged(nameof(CurrentContent));
                OnPropertyChanged(nameof(CurrentIndexText));
            }
        }

        public int Count => 1;

        public object CurrentHeader => _header;

        public object CurrentContent => _desc;

        // Must be string, not object
        public string CurrentIndexText => "1 of 1";
    }

    // --- Simple snippets ---
    public static class Snippets
    {
        public static ICompletionData SlashCommandSnippet()
        {
            return new SnippetCompletionData("!slash", "Slash command boilerplate",
@"SLASH_MYADDON1 = '/myaddon'
SlashCmdList['MYADDON'] = function(msg)
    print('Hello from /myaddon:', msg)
end");
        }

        public static ICompletionData Ace3Snippet()
        {
            return new SnippetCompletionData("!ace", "AceAddon-3.0 boilerplate",
@"local ADDON_NAME, ns = ...
local MyAddon = LibStub('AceAddon-3.0'):NewAddon(ADDON_NAME, 'AceConsole-3.0', 'AceEvent-3.0')

function MyAddon:OnInitialize()
    self.db = LibStub('AceDB-3.0'):New(ADDON_NAME..'DB', { profile = {} }, true)
end

function MyAddon:OnEnable()
    self:Print('Enabled!')
end

function MyAddon:OnDisable()
    self:Print('Disabled!')
end");
        }
    }

    public class SnippetCompletionData : ICompletionData
    {
        private readonly string _insert;
        private readonly string _title;
        private readonly string _desc;

        public SnippetCompletionData(string trigger, string title, string insert)
        {
            Text = trigger;
            _title = title;
            _insert = insert;
            _desc = insert;
        }

        public System.Windows.Media.ImageSource? Image => null;
        public string Text { get; }
        public object Content => $"{Text}  —  {_title}";
        public object Description => _desc;
        public double Priority => 10;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            // replace trigger with snippet body
            textArea.Document.Replace(completionSegment, _insert);

            // put caret at end of inserted text
            textArea.Caret.Offset = completionSegment.Offset + _insert.Length;
            textArea.Focus();
        }
    }
}
