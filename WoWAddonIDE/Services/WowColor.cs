// Services/WowColor.cs
using System;
using System.Globalization;
using Media = System.Windows.Media;

namespace WoWAddonIDE.Services
{
    public static class WowColor
    {
        /// <summary>Parses a WoW color escape like "|cAARRGGBB". Returns false if invalid.</summary>
        public static bool TryParse(string code, out Media.Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(code)) return false;
            if (code.Length != 10 || !code.StartsWith("|c", StringComparison.Ordinal)) return false;

            var hex = code.Substring(2, 8);
            if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb)) return false;

            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >> 8) & 0xFF);
            byte b = (byte)(argb & 0xFF);

            color = Media.Color.FromArgb(a, r, g, b);
            return true;
        }

        /// <summary>Builds a WoW color escape from a WPF color.</summary>
        public static string ToWowCode(Media.Color c) => $"|c{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        public static Media.SolidColorBrush ToBrush(string code)
        {
            return TryParse(code, out var c)
                ? new Media.SolidColorBrush(c)
                : new Media.SolidColorBrush(Media.Colors.Transparent);
        }
    }
}