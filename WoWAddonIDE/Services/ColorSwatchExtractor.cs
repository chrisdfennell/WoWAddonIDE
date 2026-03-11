// Services/ColorSwatchExtractor.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Media = System.Windows.Media;

namespace WoWAddonIDE.Services
{
    /// <summary>
    /// Scans editor text for color literals:
    ///  - #RRGGBB / #AARRGGBB
    ///  - WoW |cAARRGGBB codes (e.g., "|cFFFF8888")
    /// </summary>
    public static class ColorSwatchExtractor
    {
        private static readonly Regex HexRx =
            new(@"#(?<hex>[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})", RegexOptions.Compiled);

        private static readonly Regex WowRx =
            new(@"\|c(?<hex>[0-9A-Fa-f]{8})", RegexOptions.Compiled); // |cAARRGGBB

        public static IList<Media.Color> BuildSwatches(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<Media.Color>();

            var set = new HashSet<Media.Color>();
            foreach (var c in ExtractHexColors(text)) set.Add(c);
            foreach (var c in ExtractWowColors(text)) set.Add(c);
            return set.ToList();
        }

        public static IEnumerable<Media.Color> ExtractHexColors(string text)
        {
            foreach (Match m in HexRx.Matches(text))
            {
                var hex = m.Groups["hex"].Value;
                if (hex.Length == 6)
                {
                    byte r = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber);
                    yield return Media.Color.FromRgb(r, g, b);
                }
                else // 8
                {
                    byte a = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber);
                    byte r = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber);
                    byte g = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber);
                    byte b = byte.Parse(hex.AsSpan(6, 2), NumberStyles.HexNumber);
                    yield return Media.Color.FromArgb(a, r, g, b);
                }
            }
        }

        public static IEnumerable<Media.Color> ExtractWowColors(string text)
        {
            foreach (Match m in WowRx.Matches(text))
            {
                var hex = m.Groups["hex"].Value; // AARRGGBB
                byte a = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber);
                byte r = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.AsSpan(6, 2), NumberStyles.HexNumber);
                yield return Media.Color.FromArgb(a, r, g, b);
            }
        }
    }
}