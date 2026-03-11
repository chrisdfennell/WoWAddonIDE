using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Rendering;

namespace WoWAddonIDE.Services
{
    public sealed class WowColorInlineGenerator : VisualLineElementGenerator
    {
        private static readonly Regex Rx = new(@"\|c([0-9A-Fa-f]{8})", RegexOptions.Compiled);

        // Bubbling event the MainWindow can subscribe to
        public static readonly RoutedEvent ColorSwatchClickedEvent =
            EventManager.RegisterRoutedEvent(
                "ColorSwatchClicked", RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(WowColorInlineGenerator));

        public static void AddColorSwatchClickedHandler(UIElement target, RoutedEventHandler handler) =>
            target.AddHandler(ColorSwatchClickedEvent, handler);

        public override int GetFirstInterestedOffset(int startOffset)
        {
            var last = CurrentContext.VisualLine.LastDocumentLine.EndOffset;
            var len = last - startOffset;
            if (len <= 0) return -1;

            var text = CurrentContext.Document.GetText(startOffset, len);
            var m = Rx.Match(text);
            return m.Success ? startOffset + m.Index : -1;
        }

        public override VisualLineElement? ConstructElement(int offset)
        {
            // We only need 10 chars for |cAARRGGBB
            int take = Math.Min(10, CurrentContext.Document.TextLength - offset);
            if (take < 10) return null;

            var fragment = CurrentContext.Document.GetText(offset, 10);
            var m = Rx.Match(fragment);
            if (!m.Success) return null;

            if (!uint.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
                return null;

            var color = System.Windows.Media.Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));

            var swatch = new Border
            {
                Width = 14,
                Height = 14,
                Margin = new Thickness(0, 0, 3, 0),
                BorderBrush = System.Windows.Media.Brushes.DimGray,
                BorderThickness = new Thickness(1),
                Background = new System.Windows.Media.SolidColorBrush(color),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"#{m.Groups[1].Value}"
            };

            // store the start offset so the click handler knows where to replace
            swatch.Tag = offset;

            swatch.Focusable = false;
            swatch.PreviewMouseLeftButtonDown += (s, e) =>
            {
                swatch.RaiseEvent(new RoutedEventArgs(ColorSwatchClickedEvent, swatch));
                e.Handled = true;
            };

            // 10 = length of "|c" + 8 hex digits
            return new InlineObjectElement(10, swatch);
        }
    }
}
