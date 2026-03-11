// Windows/FrameDesignerWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using WoWAddonIDE.Models.FrameXml;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfListBox = System.Windows.Controls.ListBox;
using System.Windows.Controls;

namespace WoWAddonIDE.Windows
{
    public partial class FrameDesignerWindow : Window
    {
        private readonly List<FrameDefinition> _rootFrames = new();
        private FrameDefinition? _selectedFrame;
        private readonly Action<string>? _insertCallback;

        public FrameDesignerWindow()
        {
            InitializeComponent();
        }

        public FrameDesignerWindow(Action<string>? insertCallback = null) : this()
        {
            _insertCallback = insertCallback;
        }

        // ==================== Frame Management ====================

        private void AddFrame_Click(object sender, RoutedEventArgs e)
        {
            var type = PickFrameType();
            if (type == null) return;

            var frame = new FrameDefinition
            {
                FrameType = type,
                Name = $"My{type}",
                Anchors = { new AnchorPoint { Point = "CENTER" } }
            };
            _rootFrames.Add(frame);
            RefreshTree();
            SelectFrame(frame);
        }

        private void AddChild_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFrame == null) { MessageBox.Show(this, "Select a parent frame first.", "Add Child"); return; }

            var type = PickFrameType();
            if (type == null) return;

            var child = new FrameDefinition
            {
                FrameType = type,
                Name = $"{_selectedFrame.Name}_{type}",
                Anchors = { new AnchorPoint { Point = "CENTER" } }
            };
            _selectedFrame.Children.Add(child);
            RefreshTree();
            SelectFrame(child);
        }

        private void RemoveFrame_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFrame == null) return;

            if (MessageBox.Show(this, $"Remove '{_selectedFrame.Name}'?", "Remove Frame",
                    MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            if (_rootFrames.Remove(_selectedFrame))
            {
                _selectedFrame = null;
            }
            else
            {
                // Search and remove from parent
                foreach (var root in _rootFrames)
                    if (RemoveChildRecursive(root, _selectedFrame))
                    { _selectedFrame = null; break; }
            }

            RefreshTree();
            PropertiesPanel.Children.Clear();
            UpdateXmlPreview();
        }

        private static bool RemoveChildRecursive(FrameDefinition parent, FrameDefinition target)
        {
            if (parent.Children.Remove(target)) return true;
            foreach (var child in parent.Children)
                if (RemoveChildRecursive(child, target)) return true;
            return false;
        }

        private void AddAnchor_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFrame == null) return;
            _selectedFrame.Anchors.Add(new AnchorPoint { Point = "CENTER" });
            BuildPropertyForm(_selectedFrame);
            UpdateXmlPreview();
        }

        private void AddScript_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFrame == null) return;

            var scripts = FrameTypeSchema.GetScriptsFor(_selectedFrame.FrameType);
            var existing = new HashSet<string>(_selectedFrame.Scripts.Select(s => s.Handler));
            var available = scripts.Where(s => !existing.Contains(s)).ToArray();

            if (available.Length == 0) { MessageBox.Show(this, "All common scripts already added."); return; }

            var picked = PickFromList("Add Script Handler", available);
            if (picked == null) return;

            _selectedFrame.Scripts.Add(new FrameScript { Handler = picked, Body = $"-- {picked} handler\n" });
            BuildPropertyForm(_selectedFrame);
            UpdateXmlPreview();
        }

        // ==================== Tree ====================

        private void RefreshTree()
        {
            FrameTree.Items.Clear();
            foreach (var frame in _rootFrames)
                FrameTree.Items.Add(BuildTreeNode(frame));
        }

        private TreeViewItem BuildTreeNode(FrameDefinition frame)
        {
            var display = string.IsNullOrWhiteSpace(frame.Name)
                ? $"({frame.FrameType})"
                : $"{frame.Name} [{frame.FrameType}]";

            var node = new TreeViewItem
            {
                Header = display,
                Tag = frame,
                IsExpanded = true
            };

            foreach (var child in frame.Children)
                node.Items.Add(BuildTreeNode(child));

            return node;
        }

        private void SelectFrame(FrameDefinition frame)
        {
            var node = FindNode(FrameTree.Items, frame);
            if (node != null) node.IsSelected = true;
        }

        private TreeViewItem? FindNode(ItemCollection items, FrameDefinition target)
        {
            foreach (var item in items)
            {
                if (item is TreeViewItem tvi)
                {
                    if (ReferenceEquals(tvi.Tag, target)) return tvi;
                    var child = FindNode(tvi.Items, target);
                    if (child != null) return child;
                }
            }
            return null;
        }

        private void FrameTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (FrameTree.SelectedItem is TreeViewItem tvi && tvi.Tag is FrameDefinition frame)
            {
                _selectedFrame = frame;
                BuildPropertyForm(frame);
            }
        }

        // ==================== Property Form ====================

        private void BuildPropertyForm(FrameDefinition frame)
        {
            PropertiesPanel.Children.Clear();
            var panel = PropertiesPanel;

            // Identity
            AddGroupHeader(panel, "Identity");
            AddField(panel, "Frame Type", frame.FrameType, v => { frame.FrameType = v; RefreshTree(); });
            AddField(panel, "Name", frame.Name, v => { frame.Name = v; RefreshTree(); });
            AddField(panel, "Parent", frame.Parent, v => frame.Parent = v);
            AddField(panel, "Inherits", frame.Inherits, v => frame.Inherits = v);

            // Size
            AddGroupHeader(panel, "Size");
            AddIntField(panel, "Width", frame.Width, v => frame.Width = v);
            AddIntField(panel, "Height", frame.Height, v => frame.Height = v);

            // Flags
            AddGroupHeader(panel, "Flags");
            AddCheckBox(panel, "Hidden", frame.Hidden, v => frame.Hidden = v);
            AddCheckBox(panel, "EnableMouse", frame.EnableMouse, v => frame.EnableMouse = v);
            AddCheckBox(panel, "Movable", frame.Movable, v => frame.Movable = v);
            AddCheckBox(panel, "ClampedToScreen", frame.ClampedToScreen, v => frame.ClampedToScreen = v);

            // Type-specific
            switch (frame.FrameType)
            {
                case "Button":
                case "CheckButton":
                    AddGroupHeader(panel, "Button");
                    AddField(panel, "Text", frame.Text, v => frame.Text = v);
                    AddField(panel, "NormalTexture", frame.NormalTexture, v => frame.NormalTexture = v);
                    AddField(panel, "PushedTexture", frame.PushedTexture, v => frame.PushedTexture = v);
                    AddField(panel, "HighlightTexture", frame.HighlightTexture, v => frame.HighlightTexture = v);
                    break;
                case "FontString":
                    AddGroupHeader(panel, "FontString");
                    AddField(panel, "Text", frame.Text, v => frame.Text = v);
                    AddField(panel, "Font", frame.Font, v => frame.Font = v);
                    AddIntField(panel, "Font Size", frame.FontSize, v => frame.FontSize = v);
                    break;
                case "Texture":
                    AddGroupHeader(panel, "Texture");
                    AddField(panel, "File", frame.TexturePath, v => frame.TexturePath = v);
                    break;
                case "StatusBar":
                case "Slider":
                    AddGroupHeader(panel, frame.FrameType);
                    AddIntField(panel, "Min Value", frame.MinValue, v => frame.MinValue = v);
                    AddIntField(panel, "Max Value", frame.MaxValue, v => frame.MaxValue = v);
                    AddField(panel, "Orientation", frame.Orientation, v => frame.Orientation = v);
                    break;
                case "EditBox":
                    AddGroupHeader(panel, "EditBox");
                    AddField(panel, "Text", frame.Text, v => frame.Text = v);
                    break;
            }

            // Anchors
            AddGroupHeader(panel, $"Anchors ({frame.Anchors.Count})");
            for (int idx = 0; idx < frame.Anchors.Count; idx++)
            {
                var anchor = frame.Anchors[idx];
                var anchorIdx = idx;
                var anchorPanel = new Border
                {
                    BorderBrush = System.Windows.Media.Brushes.Gray,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 0, 0, 4),
                    Padding = new Thickness(4)
                };
                var ap = new StackPanel();
                AddComboField(ap, $"Point [{idx}]", AnchorPoints.All, anchor.Point, v => { anchor.Point = v; UpdateXmlPreview(); });
                AddField(ap, "RelativeTo", anchor.RelativeTo, v => { anchor.RelativeTo = v; UpdateXmlPreview(); });
                AddComboField(ap, "RelativePoint", AnchorPoints.All, anchor.RelativePoint, v => { anchor.RelativePoint = v; UpdateXmlPreview(); });
                AddIntField(ap, "X", anchor.X, v => { anchor.X = v; UpdateXmlPreview(); });
                AddIntField(ap, "Y", anchor.Y, v => { anchor.Y = v; UpdateXmlPreview(); });

                var removeBtn = new WpfButton { Content = "Remove Anchor", Margin = new Thickness(0, 2, 0, 0), Padding = new Thickness(4, 2, 4, 2) };
                removeBtn.Click += (s, e) => { frame.Anchors.RemoveAt(anchorIdx); BuildPropertyForm(frame); UpdateXmlPreview(); };
                ap.Children.Add(removeBtn);

                anchorPanel.Child = ap;
                panel.Children.Add(anchorPanel);
            }

            // Scripts
            AddGroupHeader(panel, $"Scripts ({frame.Scripts.Count})");
            for (int idx = 0; idx < frame.Scripts.Count; idx++)
            {
                var script = frame.Scripts[idx];
                var scriptIdx = idx;
                AddField(panel, script.Handler, script.Body, v => { script.Body = v; UpdateXmlPreview(); }, multiline: true);

                var removeBtn = new WpfButton { Content = $"Remove {script.Handler}", Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(4, 2, 4, 2) };
                removeBtn.Click += (s, e) => { frame.Scripts.RemoveAt(scriptIdx); BuildPropertyForm(frame); UpdateXmlPreview(); };
                panel.Children.Add(removeBtn);
            }

            UpdateXmlPreview();
        }

        // ==================== Form Helpers ====================

        private void AddGroupHeader(StackPanel panel, string text)
        {
            panel.Children.Add(new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, 8, 0, 4)
            });
        }

        private void AddField(StackPanel panel, string label, string value, Action<string> setter, bool multiline = false)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 8, 0) };
            Grid.SetColumn(lbl, 0);

            var tb = new WpfTextBox
            {
                Text = value,
                AcceptsReturn = multiline,
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                MinHeight = multiline ? 60 : 0,
                VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
            };
            tb.LostFocus += (s, e) => { setter(tb.Text); UpdateXmlPreview(); };
            Grid.SetColumn(tb, 1);

            grid.Children.Add(lbl);
            grid.Children.Add(tb);
            panel.Children.Add(grid);
        }

        private void AddIntField(StackPanel panel, string label, int value, Action<int> setter)
        {
            AddField(panel, label, value.ToString(), v => { if (int.TryParse(v, out var n)) setter(n); });
        }

        private void AddCheckBox(StackPanel panel, string label, bool value, Action<bool> setter)
        {
            var cb = new WpfCheckBox
            {
                Content = label,
                IsChecked = value,
                Margin = new Thickness(0, 0, 0, 4)
            };
            cb.Checked += (s, e) => { setter(true); UpdateXmlPreview(); };
            cb.Unchecked += (s, e) => { setter(false); UpdateXmlPreview(); };
            panel.Children.Add(cb);
        }

        private void AddComboField(StackPanel panel, string label, string[] options, string current, Action<string> setter)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(lbl, 0);

            var cb = new WpfComboBox { IsEditable = true, Text = current };
            foreach (var opt in options) cb.Items.Add(opt);
            cb.LostFocus += (s, e) => { setter(cb.Text); UpdateXmlPreview(); };
            cb.SelectionChanged += (s, e) => { if (cb.SelectedItem is string sel) { setter(sel); UpdateXmlPreview(); } };
            Grid.SetColumn(cb, 1);

            grid.Children.Add(lbl);
            grid.Children.Add(cb);
            panel.Children.Add(grid);
        }

        // ==================== XML ====================

        private void UpdateXmlPreview()
        {
            if (_rootFrames.Count == 0)
            {
                XmlPreview.Text = "<!-- Add a frame to see XML preview -->";
                return;
            }

            try
            {
                var ui = new XElement("Ui",
                    new XAttribute("xmlns", "http://www.blizzard.com/wow/ui/"));
                foreach (var frame in _rootFrames)
                    ui.Add(frame.ToXml());

                var doc = new XDocument(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", null), ui);
                XmlPreview.Text = doc.ToString();
                StatusText.Text = $"{_rootFrames.Count} root frame(s)";
            }
            catch (Exception ex)
            {
                XmlPreview.Text = $"<!-- Error: {ex.Message} -->";
            }
        }

        private string GetFullXml()
        {
            return XmlPreview.Text ?? "";
        }

        // ==================== File operations ====================

        private void LoadXml_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load WoW XML File",
                Filter = "XML Files (*.xml)|*.xml|All Files|*.*"
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var xml = File.ReadAllText(dlg.FileName);
                var frames = FrameDefinition.FromXmlDocument(xml);
                if (frames.Count == 0) { MessageBox.Show(this, "No frames found in the XML file."); return; }

                _rootFrames.Clear();
                _rootFrames.AddRange(frames);
                RefreshTree();
                UpdateXmlPreview();
                StatusText.Text = $"Loaded {frames.Count} frame(s) from {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error loading XML: {ex.Message}", "Load Error");
            }
        }

        private void SaveXml_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save WoW XML File",
                Filter = "XML Files (*.xml)|*.xml|All Files|*.*",
                FileName = "MyFrames.xml"
            };
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, GetFullXml());
                StatusText.Text = $"Saved to {Path.GetFileName(dlg.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error saving: {ex.Message}", "Save Error");
            }
        }

        private void CopyXml_Click(object sender, RoutedEventArgs e)
        {
            var xml = GetFullXml();
            if (!string.IsNullOrEmpty(xml))
                try { Clipboard.SetText(xml); StatusText.Text = "XML copied to clipboard"; } catch { }
        }

        private void InsertIntoEditor_Click(object sender, RoutedEventArgs e)
        {
            var xml = GetFullXml();
            if (!string.IsNullOrEmpty(xml) && _insertCallback != null)
            {
                _insertCallback(xml);
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        }

        // ==================== Dialogs ====================

        private string? PickFrameType()
        {
            return PickFromList("Select Frame Type", FrameTypeSchema.FrameTypes);
        }

        private string? PickFromList(string title, string[] items)
        {
            var win = new Window
            {
                Title = title,
                Width = 300, Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = System.Windows.Media.Brushes.White
            };

            var panel = new DockPanel { Margin = new Thickness(8) };
            var listBox = new WpfListBox { Margin = new Thickness(0, 0, 0, 8) };
            foreach (var item in items) listBox.Items.Add(item);
            listBox.SelectedIndex = 0;

            var okBtn = new WpfButton { Content = "OK", Width = 80, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
            DockPanel.SetDock(okBtn, Dock.Bottom);

            string? result = null;
            okBtn.Click += (s, e) => { result = listBox.SelectedItem as string; win.Close(); };
            listBox.MouseDoubleClick += (s, e) => { result = listBox.SelectedItem as string; win.Close(); };

            panel.Children.Add(okBtn);
            panel.Children.Add(listBox);
            win.Content = panel;
            win.ShowDialog();
            return result;
        }
    }
}
