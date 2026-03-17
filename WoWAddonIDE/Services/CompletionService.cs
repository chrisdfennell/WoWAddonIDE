using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
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
            "true","until","while","pairs","ipairs","require","pcall","xpcall",
            "type","table","string","math","debug"
        };

        private List<WoWApiEntry> _api = new();
        private HashSet<string> _apiNames = new(StringComparer.OrdinalIgnoreCase);

        public IHighlightingDefinition? LuaHighlight { get; private set; }

        /// <summary>All loaded API entries (for documentation browser, diagnostics, etc.).</summary>
        public IReadOnlyList<WoWApiEntry> ApiEntries => _api;

        /// <summary>All known API names (includes imported names without full entries).</summary>
        public IReadOnlyCollection<string> ApiNames => _apiNames;

        public CompletionService()
        {
            LuaHighlight = LoadLuaHighlight();
            _api = LoadWoWApi();
            _apiNames.UnionWith(_api.Select(a => a.name));
        }

        /// <summary>Replace the WoW API entries (e.g., after MoonSharp import).</summary>
        public void SetApiDocs(IEnumerable<WoWApiEntry> entries)
        {
            _api = entries?.ToList() ?? new List<WoWApiEntry>();
            _apiNames.Clear();
            _apiNames.UnionWith(_api.Select(a => a.name));
        }

        /// <summary>Optional: add extra API names without signatures/descriptions.</summary>
        public void SetApiNames(IEnumerable<string> names)
        {
            if (names == null) return;
            _apiNames.UnionWith(names);
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

                    // 2) Pack URI (update assembly name if needed)
                    s = Application.GetResourceStream(new Uri("/WoWAddonIDE;component/Resources/Lua.xshd", UriKind.Relative))?.Stream;
                    if (s != null) return s;

                    // 3) Disk next to exe (bin/Resources/Lua.xshd)
                    var disk = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Lua.xshd");
                    if (File.Exists(disk)) return File.OpenRead(disk);

                    // 4) Embedded resource fallback
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

                HighlightingManager.Instance.RegisterHighlighting("Lua", new[] { ".lua" }, def);
                return def;
            }
            catch (Exception ex)
            {
                LogService.Warn("Failed to load Lua syntax highlighting", ex);
                return null;
            }
        }

        private static List<WoWApiEntry> LoadWoWApi()
        {
            try
            {
                // Try relative pack resource
                using var s = Application.GetResourceStream(new Uri("Resources/wow_api.json", UriKind.Relative))?.Stream
                              ?? Application.GetResourceStream(new Uri("/WoWAddonIDE;component/Resources/wow_api.json", UriKind.Relative))?.Stream;

                if (s == null)
                {
                    // Disk fallback
                    var onDisk = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "wow_api.json");
                    if (File.Exists(onDisk))
                        using (var fs = File.OpenRead(onDisk))
                            return DeserializeApi(fs);
                    return new List<WoWApiEntry>();
                }

                return DeserializeApi(s);
            }
            catch (Exception ex)
            {
                LogService.Warn("Failed to load WoW API definitions", ex);
                return new List<WoWApiEntry>();
            }

            static List<WoWApiEntry> DeserializeApi(Stream stream)
            {
                using var sr = new StreamReader(stream);
                var json = sr.ReadToEnd();
                return JsonConvert.DeserializeObject<List<WoWApiEntry>>(json) ?? new List<WoWApiEntry>();
            }
        }

        /// <summary>
        /// Show a CompletionWindow with Lua keywords, WoW APIs, imported API names, and buffer words filtered by prefix.
        /// </summary>
        public void ShowCompletion(TextArea area, string currentWord)
        {
            if (area?.Document == null) return;

            var bufferWords = ExtractIdentifiers(area.Document)
                .Where(w => w.Length >= 3)
                .Take(500);

            var apiNamesFromEntries = _api.Select(a => a.name);

            var candidates = _luaKeywords
                .Concat(apiNamesFromEntries)
                .Concat(_apiNames)
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

            // Snippet triggers (prefixed with '!')
            foreach (var snippet in Snippets.GetMatchingSnippets(currentWord))
                data.Add(snippet);

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

            // char just before the trigger (e.g., '(')
            int pos = caret - 2;
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value == _selectedIndex) return;
                _selectedIndex = value;
                OnPropertyChanged(nameof(SelectedIndex));
                OnPropertyChanged(nameof(CurrentHeader));
                OnPropertyChanged(nameof(CurrentContent));
                OnPropertyChanged(nameof(CurrentIndexText));
            }
        }

        public int Count => 1;
        public object CurrentHeader => _header;
        public object CurrentContent => _desc;
        public string CurrentIndexText => "1 of 1";
    }

    // --- Snippets ---
    public static class Snippets
    {
        /// <summary>All available snippet definitions (trigger, title, body).</summary>
        public static readonly (string Trigger, string Title, string Body)[] All =
        {
            ("!slash", "Slash command", @"SLASH_MYADDON1 = '/myaddon'
SlashCmdList['MYADDON'] = function(msg)
    print('Hello from /myaddon:', msg)
end"),

            ("!ace", "AceAddon-3.0 skeleton", @"local ADDON_NAME, ns = ...
local MyAddon = LibStub('AceAddon-3.0'):NewAddon(ADDON_NAME, 'AceConsole-3.0', 'AceEvent-3.0')

function MyAddon:OnInitialize()
    self.db = LibStub('AceDB-3.0'):New(ADDON_NAME..'DB', { profile = {} }, true)
end

function MyAddon:OnEnable()
    self:Print('Enabled!')
end

function MyAddon:OnDisable()
    self:Print('Disabled!')
end"),

            ("!event", "Event handler frame", @"local f = CreateFrame('Frame')
f:RegisterEvent('ADDON_LOADED')
f:RegisterEvent('PLAYER_LOGIN')
f:SetScript('OnEvent', function(self, event, ...)
    if event == 'ADDON_LOADED' then
        local addonName = ...
        -- initialize here
    elseif event == 'PLAYER_LOGIN' then
        -- player is ready
    end
end)"),

            ("!savedvars", "SavedVariables init pattern", @"local ADDON_NAME, ns = ...

-- Defaults (deep-copied on first load)
local defaults = {
    enabled = true,
    scale = 1.0,
    position = { x = 0, y = 0 },
}

local f = CreateFrame('Frame')
f:RegisterEvent('ADDON_LOADED')
f:RegisterEvent('PLAYER_LOGOUT')
f:SetScript('OnEvent', function(self, event, ...)
    if event == 'ADDON_LOADED' and ... == ADDON_NAME then
        -- Merge saved data with defaults
        if not MyAddonDB then MyAddonDB = {} end
        for k, v in pairs(defaults) do
            if MyAddonDB[k] == nil then MyAddonDB[k] = v end
        end
        ns.db = MyAddonDB
    elseif event == 'PLAYER_LOGOUT' then
        -- Data is auto-saved by WoW; do cleanup here if needed
    end
end)"),

            ("!locale", "Localization table", @"local ADDON_NAME, ns = ...

-- Default locale (English)
local L = setmetatable({}, { __index = function(t, k) t[k] = k; return k end })
ns.L = L

-- To add translations, create locale files:
-- if GetLocale() == 'deDE' then
--     L['Hello'] = 'Hallo'
-- end"),

            ("!options", "Interface Options panel", @"local ADDON_NAME, ns = ...

local panel = CreateFrame('Frame')
panel.name = ADDON_NAME

local title = panel:CreateFontString(nil, 'ARTWORK', 'GameFontNormalLarge')
title:SetPoint('TOPLEFT', 16, -16)
title:SetText(ADDON_NAME)

local desc = panel:CreateFontString(nil, 'ARTWORK', 'GameFontHighlightSmall')
desc:SetPoint('TOPLEFT', title, 'BOTTOMLEFT', 0, -8)
desc:SetText('Configure ' .. ADDON_NAME .. ' settings.')

-- Add your checkboxes, sliders, etc. here

if Settings and Settings.RegisterCanvasLayoutCategory then
    local category = Settings.RegisterCanvasLayoutCategory(panel, ADDON_NAME)
    Settings.RegisterAddOnCategory(category)
else
    InterfaceOptions_AddCategory(panel)
end"),

            ("!minimap", "Minimap button (LibDataBroker)", @"local ADDON_NAME, ns = ...
local ldb = LibStub('LibDataBroker-1.1')
local icon = LibStub('LibDBIcon-1.0')

local broker = ldb:NewDataObject(ADDON_NAME, {
    type = 'launcher',
    icon = 'Interface\\Icons\\INV_Misc_QuestionMark',
    OnClick = function(self, button)
        if button == 'LeftButton' then
            -- toggle main window
        elseif button == 'RightButton' then
            -- open settings
        end
    end,
    OnTooltipShow = function(tooltip)
        tooltip:AddLine(ADDON_NAME)
        tooltip:AddLine('Left-click to toggle', 1, 1, 1)
        tooltip:AddLine('Right-click for settings', 1, 1, 1)
    end,
})

-- In your ADDON_LOADED handler:
-- if not MyAddonDB then MyAddonDB = {} end
-- if not MyAddonDB.minimap then MyAddonDB.minimap = {} end
-- icon:Register(ADDON_NAME, broker, MyAddonDB.minimap)"),

            ("!comm", "Addon communication (CHAT_MSG_ADDON)", @"local ADDON_NAME, ns = ...
local PREFIX = ADDON_NAME

C_ChatInfo.RegisterAddonMessagePrefix(PREFIX)

local f = CreateFrame('Frame')
f:RegisterEvent('CHAT_MSG_ADDON')
f:SetScript('OnEvent', function(self, event, prefix, message, channel, sender)
    if prefix ~= PREFIX then return end
    -- Handle incoming message
    print(format('[%s] %s: %s', prefix, sender, message))
end)

-- Send a message:
-- C_ChatInfo.SendAddonMessage(PREFIX, 'hello', 'PARTY')
-- C_ChatInfo.SendAddonMessage(PREFIX, 'hello', 'WHISPER', targetName)"),

            ("!securehook", "Secure hook pattern", @"-- Hook a global function without tainting it
hooksecurefunc('FunctionName', function(...)
    -- Your code runs AFTER the original function
end)

-- Hook a method on a frame
hooksecurefunc(GameTooltip, 'SetUnitBuff', function(self, ...)
    -- Runs after SetUnitBuff
end)"),

            ("!frame", "Basic frame with backdrop", @"local f = CreateFrame('Frame', 'MyAddonFrame', UIParent, 'BackdropTemplate')
f:SetSize(300, 200)
f:SetPoint('CENTER')
f:SetBackdrop({
    bgFile = 'Interface\\DialogFrame\\UI-DialogBox-Background',
    edgeFile = 'Interface\\DialogFrame\\UI-DialogBox-Border',
    edgeSize = 16,
    insets = { left = 4, right = 4, top = 4, bottom = 4 },
})
f:SetBackdropColor(0, 0, 0, 0.8)
f:SetMovable(true)
f:EnableMouse(true)
f:RegisterForDrag('LeftButton')
f:SetScript('OnDragStart', f.StartMoving)
f:SetScript('OnDragStop', f.StopMovingOrSizing)

local title = f:CreateFontString(nil, 'OVERLAY', 'GameFontNormal')
title:SetPoint('TOP', 0, -10)
title:SetText('My Frame')

local close = CreateFrame('Button', nil, f, 'UIPanelCloseButton')
close:SetPoint('TOPRIGHT', -2, -2)"),
        };

        public static ICompletionData SlashCommandSnippet() =>
            new SnippetCompletionData(All[0].Trigger, All[0].Title, All[0].Body);

        public static ICompletionData Ace3Snippet() =>
            new SnippetCompletionData(All[1].Trigger, All[1].Title, All[1].Body);

        /// <summary>Get all snippet completion items matching the given prefix.</summary>
        public static IEnumerable<ICompletionData> GetMatchingSnippets(string prefix)
        {
            foreach (var (trigger, title, body) in All)
            {
                if (trigger.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    yield return new SnippetCompletionData(trigger, title, body);
            }
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
            textArea.Document.Replace(completionSegment, _insert);
            textArea.Caret.Offset = completionSegment.Offset + _insert.Length;
            textArea.Focus();
        }
    }
}