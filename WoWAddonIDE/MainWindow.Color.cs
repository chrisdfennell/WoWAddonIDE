using System;
using System.Globalization;
using System.Windows;
using WoWAddonIDE.Services;

// Explicitly alias the System.Windows.Media namespace to avoid ambiguity
using Media = System.Windows.Media;

namespace WoWAddonIDE
{
    public partial class MainWindow : Window
    {
        private void WireWowColorClick(ICSharpCode.AvalonEdit.TextEditor editor)
        {
            editor.TextArea.TextView.AddHandler(
                WowColorInlineGenerator.ColorSwatchClickedEvent,
                new RoutedEventHandler((s, e) =>
                {
                    if (e.OriginalSource is not FrameworkElement fe) return;
                    if (fe.Tag is not int start) return;
                    if (start + 10 > editor.Document.TextLength) return;

                    string hex = editor.Document.GetText(start + 2, 8);
                    if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb)) return;

                    var current = Media.Color.FromArgb(
                        (byte)((argb >> 24) & 0xFF),
                        (byte)((argb >> 16) & 0xFF),
                        (byte)((argb >> 8) & 0xFF),
                        (byte)(argb & 0xFF));

                    var picker = new Windows.ColorPickerWindow(current) { Owner = this };
                    if (picker.ShowDialog() == true)
                    {
                        var chosen = picker.SelectedColor;
                        string newHex = $"{chosen.A:X2}{chosen.R:X2}{chosen.G:X2}{chosen.B:X2}";
                        editor.Document.Replace(start, 10, "|c" + newHex);

                        RefreshColorSwatches(editor.Text);
                        Status($"Color updated to #{newHex}");
                    }

                    e.Handled = true;
                }),
                true
            );
        }

        private static bool TryGetWowColorAtOffset(string text, int offset, out int prefixStart, out string argbHex)
        {
            prefixStart = -1; argbHex = "";
            if (string.IsNullOrEmpty(text)) return false;

            int probe = Math.Min(Math.Max(offset, 0), text.Length);
            int cIdx = text.LastIndexOf("|c", probe, StringComparison.Ordinal);
            if (cIdx < 0 || cIdx + 10 > text.Length) return false;

            string hex = text.Substring(cIdx + 2, 8);
            if (!IsHex8(hex)) return false;

            int rIdx = text.IndexOf("|r", cIdx + 10, StringComparison.Ordinal);
            if (rIdx != -1 && offset > rIdx) return false;

            prefixStart = cIdx;
            argbHex = hex.ToUpperInvariant();
            return true;
        }

        private static bool IsHex8(string s)
        {
            if (s == null || s.Length != 8) return false;
            for (int i = 0; i < 8; i++)
            {
                char c = s[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
        }

        private static Media.Color? PickColor(Media.Color initial)
        {
            var dlg = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                AllowFullOpen = true,
                AnyColor = true,
                Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B)
            };
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK)
            {
                var c = dlg.Color;
                return Media.Color.FromArgb((byte)c.A, (byte)c.R, (byte)c.G, (byte)c.B);
            }
            return null;
        }

        private static bool TryGetWowColorHexAt(string text, int offset, out int hexStart)
        {
            hexStart = -1;
            if (string.IsNullOrEmpty(text)) return false;

            int probeStart = Math.Max(0, offset - 16);
            int pipeIdx = text.LastIndexOf("|c", offset >= 1 ? offset - 1 : 0,
                                            (offset >= 1 ? offset - 1 : 0) - probeStart + 1,
                                            StringComparison.Ordinal);
            if (pipeIdx < 0) return false;

            int h = pipeIdx + 2;
            if (h + 8 > text.Length) return false;

            for (int i = 0; i < 8; i++)
            {
                char ch = text[h + i];
                if (!Uri.IsHexDigit(ch)) return false;
            }

            int rIdx = text.IndexOf("|r", h + 8, StringComparison.Ordinal);
            if (rIdx < 0) return false;

            if (offset < pipeIdx || offset > rIdx) return false;

            hexStart = h;
            return true;
        }

        private static bool TryParseARGB(string hex, out byte A, out byte R, out byte G, out byte B)
        {
            A = R = G = B = 0;
            if (hex?.Length != 8) return false;
            try
            {
                A = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                R = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                G = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                B = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                return true;
            }
            catch { return false; }
        }

        private void RefreshColorSwatches(string text)
        {
            var colors = ColorSwatchExtractor.BuildSwatches(text);

            if (SwatchWrap != null)
            {
                SwatchWrap.Children.Clear();
                foreach (var c in colors)
                {
                    SwatchWrap.Children.Add(new System.Windows.Controls.Border
                    {
                        Width = 18,
                        Height = 18,
                        Margin = new Thickness(2),
                        Background = new Media.SolidColorBrush(Media.Color.FromArgb(c.A, c.R, c.G, c.B)),
                        BorderBrush = Media.Brushes.DimGray,
                        BorderThickness = new Thickness(1),
                        ToolTip = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}"
                    });
                }
            }
        }
    }
}