// File: MainWindow.Minimap.cs
using ICSharpCode.AvalonEdit;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Media = System.Windows.Media;
using WoWAddonIDE.Services;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private TextEditor? _minimapEditor;
        private System.Windows.Shapes.Rectangle? _minimapViewport;
        private Canvas? _minimapOverlay;
        private bool _minimapVisible = true;

        private void Minimap_Init()
        {
            if (MinimapBorder == null) return;

            var container = new Grid();

            _minimapEditor = new TextEditor
            {
                IsReadOnly = true,
                ShowLineNumbers = false,
                FontSize = 1,
                FontFamily = new Media.FontFamily("Consolas"),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                IsHitTestVisible = false,
                Background = Media.Brushes.Transparent,
                Foreground = new Media.SolidColorBrush(Media.Color.FromArgb(0x80, 0x80, 0x80, 0x80)),
                BorderThickness = new Thickness(0)
            };
            _minimapEditor.Options.EnableHyperlinks = false;
            _minimapEditor.Options.HighlightCurrentLine = false;

            _minimapOverlay = new Canvas { Background = Media.Brushes.Transparent };
            _minimapViewport = new System.Windows.Shapes.Rectangle
            {
                Fill = new Media.SolidColorBrush(Media.Color.FromArgb(0x30, 0x89, 0xB4, 0xFA)),
                Stroke = new Media.SolidColorBrush(Media.Color.FromArgb(0x60, 0x89, 0xB4, 0xFA)),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            _minimapOverlay.Children.Add(_minimapViewport);

            _minimapOverlay.MouseLeftButtonDown += Minimap_MouseDown;
            _minimapOverlay.MouseMove += Minimap_MouseDrag;
            _minimapOverlay.IsHitTestVisible = true;

            container.Children.Add(_minimapEditor);
            container.Children.Add(_minimapOverlay);

            MinimapBorder.Child = container;
            ThemeManager.ApplyToEditor(_minimapEditor);
            _minimapEditor.FontSize = 1;
            _minimapEditor.ShowLineNumbers = false;
        }

        internal void Minimap_SyncToActiveEditor()
        {
            if (_minimapEditor == null) return;

            var ed = ActiveEditor();
            if (ed == null)
            {
                _minimapEditor.Text = "";
                return;
            }

            if (_minimapEditor.Text != ed.Text)
            {
                _minimapEditor.Text = ed.Text;
                _minimapEditor.SyntaxHighlighting = ed.SyntaxHighlighting;
                ThemeManager.ApplyToEditor(_minimapEditor);
                _minimapEditor.FontSize = 1;
                _minimapEditor.ShowLineNumbers = false;
            }

            Minimap_UpdateViewport(ed);
        }

        private void Minimap_UpdateViewport(TextEditor ed)
        {
            if (_minimapViewport == null || _minimapOverlay == null || _minimapEditor == null) return;

            var totalLines = Math.Max(1, ed.Document.LineCount);
            var firstVisible = ed.TextArea.TextView.GetDocumentLineByVisualTop(
                ed.TextArea.TextView.ScrollOffset.Y)?.LineNumber ?? 1;
            var visibleLines = ed.TextArea.TextView.DefaultLineHeight > 0
                ? (int)(ed.TextArea.TextView.ActualHeight / ed.TextArea.TextView.DefaultLineHeight)
                : 20;

            var overlayHeight = _minimapOverlay.ActualHeight;
            if (overlayHeight <= 0) overlayHeight = MinimapBorder?.ActualHeight ?? 0;
            if (overlayHeight <= 0) return;

            var lineHeight = overlayHeight / totalLines;
            var top = (firstVisible - 1) * lineHeight;
            var height = Math.Max(4, visibleLines * lineHeight);

            Canvas.SetTop(_minimapViewport, top);
            _minimapViewport.Width = _minimapOverlay.ActualWidth;
            _minimapViewport.Height = height;
        }

        private void Minimap_ScrollToPosition(System.Windows.Point pos)
        {
            var ed = ActiveEditor();
            if (ed == null || _minimapOverlay == null) return;

            var totalLines = Math.Max(1, ed.Document.LineCount);
            var overlayHeight = _minimapOverlay.ActualHeight;
            if (overlayHeight <= 0) return;

            var targetLine = (int)(pos.Y / overlayHeight * totalLines) + 1;
            targetLine = Math.Clamp(targetLine, 1, ed.Document.LineCount);
            ed.ScrollToLine(targetLine);
            Minimap_UpdateViewport(ed);
        }

        private void Minimap_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Minimap_ScrollToPosition(e.GetPosition(_minimapOverlay));
        }

        private void Minimap_MouseDrag(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                Minimap_ScrollToPosition(e.GetPosition(_minimapOverlay));
        }

        private void Minimap_Toggle()
        {
            _minimapVisible = !_minimapVisible;
            if (MinimapBorder != null)
                MinimapBorder.Visibility = _minimapVisible ? Visibility.Visible : Visibility.Collapsed;
            Status($"Minimap: {(_minimapVisible ? "ON" : "OFF")}");
        }

        internal void Minimap_HookEditorScroll(TextEditor editor)
        {
            editor.TextArea.TextView.ScrollOffsetChanged += (s, e) =>
            {
                if (editor == ActiveEditor())
                    Minimap_UpdateViewport(editor);
            };
        }
    }
}
