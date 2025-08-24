// Services/ColorSwatchGenerator.cs
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;

// file-local aliases to avoid type collisions
using SWM = System.Windows.Media;
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace WoWAddonIDE.Services
{
    /// <summary>
    /// Inserts a small clickable color swatch before #RRGGBB / #AARRGGBB tokens.
    /// Clicking opens a color picker and replaces the hex in the document.
    /// </summary>
    public sealed class ColorSwatchGenerator : VisualLineElementGenerator
    {
        private readonly TextEditor _editor;

        // Matches #RRGGBB or #AARRGGBB
        private static readonly Regex HexRegex =
            new(@"#(?<hex>[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public ColorSwatchGenerator(TextEditor editor)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            var doc = CurrentContext?.Document;
            var line = CurrentContext?.VisualLine;
            if (doc == null || line == null) return -1;

            int endOffset = line.LastDocumentLine.EndOffset;
            if (endOffset <= startOffset) return -1;

            string text = doc.GetText(startOffset, endOffset - startOffset);
            var m = HexRegex.Match(text);
            return m.Success ? startOffset + m.Index : -1;
        }

        public override VisualLineElement? ConstructElement(int offset)
        {
            var ctx = CurrentContext;
            var doc = ctx?.Document;
            var vline = ctx?.VisualLine;
            if (doc == null || vline == null) return null;

            int tailLen = vline.LastDocumentLine.EndOffset - offset;
            if (tailLen <= 0) return null;

            string tail = doc.GetText(offset, tailLen);
            var m = HexRegex.Match(tail);

            // must start exactly at current offset
            if (!m.Success || m.Index != 0) return null;

            string token = m.Value;    // "#FF00AA33" or "#00AA33"
            int tokenLen = m.Length;
            int tokenOffset = offset;

            var swatch = new Border
            {
                Width = 12,
                Height = 12,
                Margin = new Thickness(4, 0, 4, -2),
                BorderThickness = new Thickness(1),
                BorderBrush = SWM.Brushes.Gray,
                Background = new SWM.SolidColorBrush(ParseWpfColor(token))
            };

            swatch.ToolTip = new ToolTip
            {
                Content = $"Click to pick color for {token}"
            };

            swatch.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;

                try
                {
                    // Prefer WinForms ColorDialog (good UX, built-in)
                    var start = ParseWpfColor(token);
                    SD.Color startDrawing = SD.Color.FromArgb(start.A, start.R, start.G, start.B);

                    using var dlg = new WF.ColorDialog
                    {
                        AllowFullOpen = true,
                        FullOpen = true,
                        AnyColor = true,
                        Color = startDrawing
                    };

                    if (dlg.ShowDialog() == WF.DialogResult.OK)
                    {
                        SD.Color c = dlg.Color;
                        var chosen = SWM.Color.FromArgb(c.A, c.R, c.G, c.B);

                        string newHex = token.Length == 7
                            ? $"#{chosen.R:X2}{chosen.G:X2}{chosen.B:X2}"
                            : $"#{chosen.A:X2}{chosen.R:X2}{chosen.G:X2}{chosen.B:X2}";

                        _editor.Document.Replace(tokenOffset, tokenLen, newHex);
                    }
                }
                catch
                {
                    // Lightweight fallback palette popup
                    var popup = new Popup
                    {
                        Placement = PlacementMode.MousePoint,
                        StaysOpen = false
                    };

                    var grid = new UniformGrid { Rows = 2, Columns = 8, Margin = new Thickness(6) };
                    foreach (var c in new[]
                    {
                        SWM.Colors.Black, SWM.Colors.White, SWM.Colors.Red, SWM.Colors.Orange,
                        SWM.Colors.Yellow, SWM.Colors.Green, SWM.Colors.Cyan, SWM.Colors.Blue,
                        SWM.Colors.Indigo, SWM.Colors.Violet, SWM.Colors.Brown, SWM.Colors.Gray,
                        SWM.Colors.LightGray, SWM.Colors.Silver, SWM.Colors.Gold, SWM.Colors.Pink
                    })
                    {
                        var chip = new Border
                        {
                            Width = 18,
                            Height = 18,
                            Margin = new Thickness(2),
                            Background = new SWM.SolidColorBrush(c),
                            BorderBrush = SWM.Brushes.DimGray,
                            BorderThickness = new Thickness(1),
                            ToolTip = $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                        };
                        chip.MouseLeftButtonDown += (_, __) =>
                        {
                            string newHex = token.Length == 7
                                ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
                                : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                            _editor.Document.Replace(tokenOffset, tokenLen, newHex);
                            popup.IsOpen = false;
                        };
                        grid.Children.Add(chip);
                    }

                    popup.Child = new Border
                    {
                        Background = SWM.Brushes.White,
                        BorderBrush = SWM.Brushes.Gray,
                        BorderThickness = new Thickness(1),
                        Child = grid
                    };
                    popup.IsOpen = true;
                }
            };

            // 0-length inline element: insert UI just before the token
            return new InlineObjectElement(0, swatch);
        }

        private static SWM.Color ParseWpfColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex[0] != '#')
                return SWM.Colors.Transparent;

            try
            {
                byte a = 0xFF, r, g, b;

                if (hex.Length == 7) // #RRGGBB
                {
                    r = byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber);
                }
                else if (hex.Length == 9) // #AARRGGBB
                {
                    a = byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber);
                    r = byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber);
                    g = byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber);
                    b = byte.Parse(hex.AsSpan(7, 2), NumberStyles.HexNumber);
                }
                else
                {
                    return SWM.Colors.Transparent;
                }

                return SWM.Color.FromArgb(a, r, g, b);
            }
            catch
            {
                return SWM.Colors.Transparent;
            }
        }
    }
}